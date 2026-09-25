using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Microsoft.Extensions.Time.Testing;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;

namespace Tests.Storage.Blobs;

[TestClass]
public class BlobContainerLeaseClientTests
{
    [TestMethod]
    public void Lease_Lifecycle_Should_Update_Container_State()
    {
        var containerClient = CreateContainerClient();
        var leaseClient = containerClient.GetBlobLeaseClient();

        var acquired = leaseClient.Acquire(BlobLeaseClient.InfiniteLeaseDuration);

        acquired.GetRawResponse().Status.Should().Be(201);
        acquired.Value.LeaseId.Should().Be(leaseClient.LeaseId);
        acquired.GetRawResponse().Headers.TryGetValue("x-ms-lease-id", out var leaseIdHeader).Should().BeTrue();
        leaseIdHeader.Should().Be(leaseClient.LeaseId);
        AssertLeaseProperties(containerClient, LeaseState.Leased, LeaseStatus.Locked, LeaseDurationType.Infinite);

        containerClient.GetProperties(new BlobRequestConditions { LeaseId = leaseClient.LeaseId }).Value.LeaseState.Should().Be(LeaseState.Leased);

        var getPropertiesWithWrongLease = () => containerClient.GetProperties(
            new BlobRequestConditions { LeaseId = Guid.NewGuid().ToString() });
        getPropertiesWithWrongLease.Should().Throw<RequestFailedException>()
            .Where(e => e.Status == 412 && e.ErrorCode == "LeaseIdMismatchWithContainerOperation");

        var competingClient = containerClient.GetBlobLeaseClient();
        var competingAcquire = () => competingClient.Acquire(BlobLeaseClient.InfiniteLeaseDuration);
        competingAcquire.Should().Throw<RequestFailedException>()
            .Where(e => e.Status == 409 && e.ErrorCode == "LeaseAlreadyPresent");

        leaseClient.Renew().Value.LeaseId.Should().Be(leaseClient.LeaseId);

        var previousLeaseId = leaseClient.LeaseId;
        var proposedLeaseId = Guid.NewGuid().ToString();
        leaseClient.Change(proposedLeaseId).Value.LeaseId.Should().Be(proposedLeaseId);
        leaseClient.LeaseId.Should().Be(proposedLeaseId);

        var previousLeaseClient = containerClient.GetBlobLeaseClient(previousLeaseId);
        var previousLeaseRenew = () => previousLeaseClient.Renew();
        previousLeaseRenew.Should().Throw<RequestFailedException>()
            .Where(e => e.Status == 409 && e.ErrorCode == "LeaseIdMismatchWithLeaseOperation");

        leaseClient.Release().GetRawResponse().Status.Should().Be(200);
        AssertLeaseProperties(containerClient, LeaseState.Available, LeaseStatus.Unlocked, null);
    }

    [TestMethod]
    public void Fixed_Lease_Should_Expire_And_Remain_Renewable()
    {
        var timeProvider = new FakeTimeProvider();
        var containerClient = CreateContainerClient(timeProvider);
        var leaseClient = containerClient.GetBlobLeaseClient();

        leaseClient.Acquire(TimeSpan.FromSeconds(15));
        AssertLeaseProperties(containerClient, LeaseState.Leased, LeaseStatus.Locked, LeaseDurationType.Fixed);

        timeProvider.Advance(TimeSpan.FromSeconds(15));
        AssertLeaseProperties(containerClient, LeaseState.Expired, LeaseStatus.Unlocked, null);

        leaseClient.Renew();
        AssertLeaseProperties(containerClient, LeaseState.Leased, LeaseStatus.Locked, LeaseDurationType.Fixed);

        timeProvider.Advance(TimeSpan.FromSeconds(15));
        var replacementClient = containerClient.GetBlobLeaseClient();
        replacementClient.Acquire(TimeSpan.FromSeconds(30));

        var renewOldLease = () => leaseClient.Renew();
        renewOldLease.Should().Throw<RequestFailedException>()
            .Where(e => e.Status == 409 && e.ErrorCode == "LeaseIdMismatchWithLeaseOperation");
    }

    [TestMethod]
    public void Break_Should_Block_Acquire_Until_Break_Period_Ends()
    {
        var timeProvider = new FakeTimeProvider();
        var containerClient = CreateContainerClient(timeProvider);
        var leaseClient = containerClient.GetBlobLeaseClient();

        leaseClient.Acquire(BlobLeaseClient.InfiniteLeaseDuration);
        var breakResponse = leaseClient.Break(TimeSpan.FromSeconds(10));
        breakResponse.GetRawResponse().Status.Should().Be(202);
        breakResponse.GetRawResponse().Headers.TryGetValue("x-ms-lease-time", out var leaseTimeHeader).Should().BeTrue();
        leaseTimeHeader.Should().Be("10");
        AssertLeaseProperties(containerClient, LeaseState.Breaking, LeaseStatus.Locked, null);

        var acquireWhileBreaking = () => containerClient.GetBlobLeaseClient().Acquire(TimeSpan.FromSeconds(15));
        acquireWhileBreaking.Should().Throw<RequestFailedException>()
            .Where(e => e.Status == 409 && e.ErrorCode == "LeaseIsBreakingAndCannotBeAcquired");

        timeProvider.Advance(TimeSpan.FromSeconds(10));
        AssertLeaseProperties(containerClient, LeaseState.Broken, LeaseStatus.Unlocked, null);

        containerClient.GetBlobLeaseClient().Acquire(TimeSpan.FromSeconds(15));
        AssertLeaseProperties(containerClient, LeaseState.Leased, LeaseStatus.Locked, LeaseDurationType.Fixed);
    }

    [TestMethod]
    public async Task Async_Lease_Lifecycle_Should_Succeed()
    {
        var containerClient = CreateContainerClient();
        var leaseClient = containerClient.GetBlobLeaseClient();

        (await leaseClient.AcquireAsync(TimeSpan.FromSeconds(15))).GetRawResponse().Status.Should().Be(201);
        (await leaseClient.RenewAsync()).GetRawResponse().Status.Should().Be(200);
        (await leaseClient.ChangeAsync(Guid.NewGuid().ToString())).GetRawResponse().Status.Should().Be(200);
        (await leaseClient.BreakAsync(TimeSpan.Zero)).GetRawResponse().Status.Should().Be(202);
        (await leaseClient.ReleaseAsync()).GetRawResponse().Status.Should().Be(200);
    }

    [TestMethod]
    public void Lease_Should_Not_Restrict_Blob_Operations()
    {
        var containerClient = CreateContainerClient();
        containerClient.GetBlobLeaseClient().Acquire(BlobLeaseClient.InfiniteLeaseDuration);

        containerClient.UploadBlob("blob", BinaryData.FromString("content"));

        containerClient.GetBlobClient("blob").DownloadContent().Value.Content.ToString().Should().Be("content");
    }

    [TestMethod]
    public void Active_Lease_Should_Protect_Container_Deletion()
    {
        var containerClient = CreateContainerClient();
        var leaseClient = containerClient.GetBlobLeaseClient();
        leaseClient.Acquire(BlobLeaseClient.InfiniteLeaseDuration);

        var deleteWithoutLease = () => containerClient.Delete();
        deleteWithoutLease.Should().Throw<RequestFailedException>()
            .Where(e => e.Status == 412 && e.ErrorCode == "LeaseIdMissing");

        var deleteWithWrongLease = () => containerClient.Delete(
            new BlobRequestConditions { LeaseId = Guid.NewGuid().ToString() });
        deleteWithWrongLease.Should().Throw<RequestFailedException>()
            .Where(e => e.Status == 412 && e.ErrorCode == "LeaseIdMismatchWithContainerOperation");

        containerClient.Delete(new BlobRequestConditions { LeaseId = leaseClient.LeaseId });
        containerClient.Exists().Value.Should().BeFalse();
    }

    [TestMethod]
    public void Expired_Lease_Should_Not_Protect_Container_Deletion()
    {
        var timeProvider = new FakeTimeProvider();
        var containerClient = CreateContainerClient(timeProvider);
        containerClient.GetBlobLeaseClient().Acquire(TimeSpan.FromSeconds(15));

        timeProvider.Advance(TimeSpan.FromSeconds(15));

        containerClient.Delete();
        containerClient.Exists().Value.Should().BeFalse();
    }

    [TestMethod]
    public void Invalid_Lease_Arguments_Should_Fail()
    {
        var containerClient = CreateContainerClient();

        var invalidLeaseId = () => containerClient.GetBlobLeaseClient("not-a-guid").Acquire(TimeSpan.FromSeconds(15));
        invalidLeaseId.Should().Throw<RequestFailedException>()
            .Where(e => e.Status == 400 && e.ErrorCode == "InvalidHeaderValue");

        var invalidDuration = () => containerClient.GetBlobLeaseClient().Acquire(TimeSpan.FromSeconds(14));
        invalidDuration.Should().Throw<RequestFailedException>()
            .Where(e => e.Status == 400 && e.ErrorCode == "InvalidHeaderValue");

        var leaseClient = containerClient.GetBlobLeaseClient();
        leaseClient.Acquire(BlobLeaseClient.InfiniteLeaseDuration);

        var invalidBreakPeriod = () => leaseClient.Break(TimeSpan.FromSeconds(61));
        invalidBreakPeriod.Should().Throw<RequestFailedException>()
            .Where(e => e.Status == 400 && e.ErrorCode == "InvalidHeaderValue");

        var malformedLeaseClient = containerClient.GetBlobLeaseClient("not-a-guid");
        var renewWithInvalidLeaseId = () => malformedLeaseClient.Renew();
        renewWithInvalidLeaseId.Should().Throw<RequestFailedException>()
            .Where(e => e.Status == 400 && e.ErrorCode == "InvalidHeaderValue");

        var acquireWithETagCondition = () => containerClient.GetBlobLeaseClient().Acquire(
            TimeSpan.FromSeconds(15),
            new RequestConditions { IfMatch = ETag.All });
        acquireWithETagCondition.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    public void Blob_Leases_Should_Remain_Unsupported()
    {
        var containerClient = CreateContainerClient();

        var genericBlobLease = () => containerClient.GetBlobClient("blob").GetBlobLeaseClient();
        var blockBlobLease = () => containerClient.GetBlockBlobClient("blob").GetBlobLeaseClient();

        genericBlobLease.Should().Throw<NotSupportedException>();
        blockBlobLease.Should().Throw<NotSupportedException>();
    }

    private static InMemoryBlobContainerClient CreateContainerClient(TimeProvider? timeProvider = null)
    {
        var account = new InMemoryStorageProvider(timeProvider: timeProvider).AddAccount();
        var client = InMemoryBlobContainerClient.FromAccount(account, "test-container");
        client.Create();
        return client;
    }

    private static void AssertLeaseProperties(
        InMemoryBlobContainerClient containerClient,
        LeaseState state,
        LeaseStatus status,
        LeaseDurationType? duration)
    {
        var properties = containerClient.GetProperties().Value;
        properties.LeaseState.Should().Be(state);
        properties.LeaseStatus.Should().Be(status);
        properties.LeaseDuration.Should().Be(duration);
    }
}
