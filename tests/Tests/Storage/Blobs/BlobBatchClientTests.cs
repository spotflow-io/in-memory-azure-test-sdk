using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;
using Spotflow.InMemory.Azure.Storage.Resources;

namespace Tests.Storage.Blobs;

[TestClass]
public class BlobBatchClientTests
{
    [TestMethod]
    public void Constructor_From_Service_Client_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var serviceClient = InMemoryBlobServiceClient.FromAccount(account);

        var batchClient = serviceClient.GetBlobBatchClient();

        batchClient.Uri.Should().Be(account.BlobServiceUri);
        batchClient.Provider.Should().BeSameAs(account.Provider);
    }

    [TestMethod]
    public void Constructor_From_Container_Client_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test");

        var batchClient = containerClient.GetBlobBatchClient();

        batchClient.Uri.Should().Be(containerClient.Uri);
        batchClient.Provider.Should().BeSameAs(account.Provider);
    }

    [TestMethod]
    public void FromAccount_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var batchClient = InMemoryBlobBatchClient.FromAccount(account);

        batchClient.Uri.Should().Be(account.BlobServiceUri);
    }

    [TestMethod]
    public void DeleteBlobs_Should_Delete_All_Blobs()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var containerClient = CreateContainer(account, "test", "blob1", "blob2", "blob3");

        var batchClient = containerClient.GetBlobBatchClient();

        var responses = batchClient.DeleteBlobs(
        [
            containerClient.GetBlobClient("blob1").Uri,
            containerClient.GetBlobClient("blob2").Uri,
            containerClient.GetBlobClient("blob3").Uri
        ]);

        responses.Should().HaveCount(3);
        responses.Should().AllSatisfy(response => response.Status.Should().Be(202));

        GetBlobs(containerClient).Should().BeEmpty();
    }

    [TestMethod]
    public async Task DeleteBlobsAsync_Should_Delete_All_Blobs()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var containerClient = CreateContainer(account, "test", "blob1", "blob2");

        var batchClient = containerClient.GetBlobBatchClient();

        var responses = await batchClient.DeleteBlobsAsync(
        [
            containerClient.GetBlobClient("blob1").Uri,
            containerClient.GetBlobClient("blob2").Uri
        ]);

        responses.Should().HaveCount(2);
        responses.Should().AllSatisfy(response => response.Status.Should().Be(202));

        GetBlobs(containerClient).Should().BeEmpty();
    }

    [TestMethod]
    public void DeleteBlobs_With_Blobs_From_Multiple_Containers_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var containerClient1 = CreateContainer(account, "test1", "blob1");
        var containerClient2 = CreateContainer(account, "test2", "blob2");

        var batchClient = InMemoryBlobServiceClient.FromAccount(account).GetBlobBatchClient();

        var responses = batchClient.DeleteBlobs(
        [
            containerClient1.GetBlobClient("blob1").Uri,
            containerClient2.GetBlobClient("blob2").Uri
        ]);

        responses.Should().HaveCount(2);

        GetBlobs(containerClient1).Should().BeEmpty();
        GetBlobs(containerClient2).Should().BeEmpty();
    }

    [TestMethod]
    public void DeleteBlobs_With_Missing_Blob_Should_Fail_But_Delete_Remaining_Blobs()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var containerClient = CreateContainer(account, "test", "blob1", "blob3");

        var batchClient = containerClient.GetBlobBatchClient();

        var act = () => batchClient.DeleteBlobs(
        [
            containerClient.GetBlobClient("blob1").Uri,
            containerClient.GetBlobClient("blob2").Uri,
            containerClient.GetBlobClient("blob3").Uri
        ]);

        var innerException = ShouldThrowSingleSubRequestFailure(act);

        innerException.Status.Should().Be(404);
        innerException.ErrorCode.Should().Be("BlobNotFound");

        // Batch is not atomic - the remaining blobs are deleted anyway.
        GetBlobs(containerClient).Should().BeEmpty();
    }

    [TestMethod]
    public void DeleteBlobs_With_Missing_Container_Should_Fail()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test");

        var batchClient = containerClient.GetBlobBatchClient();

        var act = () => batchClient.DeleteBlobs([containerClient.GetBlobClient("blob1").Uri]);

        var innerException = ShouldThrowSingleSubRequestFailure(act);

        innerException.Status.Should().Be(404);
        innerException.ErrorCode.Should().Be("ContainerNotFound");
    }

    [TestMethod]
    public void DeleteBlobs_With_Empty_Collection_Should_Fail()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var batchClient = InMemoryBlobBatchClient.FromAccount(account);

        var act = () => batchClient.DeleteBlobs([]);

        act.Should().Throw<ArgumentException>();
    }

    [TestMethod]
    [DataRow(256, false)]
    [DataRow(257, true)]
    public void DeleteBlobs_Should_Respect_Max_Sub_Request_Count(int blobCount, bool shouldFail)
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blobNames = Enumerable.Range(0, blobCount).Select(i => $"blob{i}").ToArray();

        var containerClient = CreateContainer(account, "test", blobNames);

        var batchClient = containerClient.GetBlobBatchClient();

        var blobUris = blobNames.Select(name => containerClient.GetBlobClient(name).Uri).ToArray();

        var act = () => batchClient.DeleteBlobs(blobUris);

        if (shouldFail)
        {
            act.Should()
                .Throw<RequestFailedException>()
                .Where(e => e.Status == 400)
                .Where(e => e.ErrorCode == "InvalidInput");

            GetBlobs(containerClient).Should().HaveCount(blobCount);
        }
        else
        {
            act.Should().NotThrow();

            GetBlobs(containerClient).Should().BeEmpty();
        }
    }

    [TestMethod]
    public void DeleteBlobs_With_Blob_From_Different_Account_Should_Fail()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();
        var otherAccount = provider.AddAccount();

        var containerClient = CreateContainer(account, "test", "blob1");
        var otherContainerClient = CreateContainer(otherAccount, "test", "blob1");

        var batchClient = InMemoryBlobServiceClient.FromAccount(account).GetBlobBatchClient();

        var act = () => batchClient.DeleteBlobs(
        [
            containerClient.GetBlobClient("blob1").Uri,
            otherContainerClient.GetBlobClient("blob1").Uri
        ]);

        act.Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 400)
            .Where(e => e.ErrorCode == "InvalidInput");

        // Nothing is deleted - the batch is rejected before any sub-request is executed.
        GetBlobs(containerClient).Should().HaveCount(1);
    }

    [TestMethod]
    public void DeleteBlobs_With_Blob_From_Different_Container_Should_Fail_For_Container_Scoped_Client()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var containerClient = CreateContainer(account, "test1", "blob1");
        var otherContainerClient = CreateContainer(account, "test2", "blob2");

        var batchClient = containerClient.GetBlobBatchClient();

        var act = () => batchClient.DeleteBlobs(
        [
            containerClient.GetBlobClient("blob1").Uri,
            otherContainerClient.GetBlobClient("blob2").Uri
        ]);

        act.Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 400)
            .Where(e => e.ErrorCode == "InvalidInput");

        GetBlobs(containerClient).Should().HaveCount(1);
    }

    [TestMethod]
    [DataRow(DeleteSnapshotsOption.IncludeSnapshots)]
    [DataRow(DeleteSnapshotsOption.OnlySnapshots)]
    public void DeleteBlobs_With_Unsupported_Snapshots_Option_Should_Result_In_Not_Supported_Exception(DeleteSnapshotsOption snapshotsOption)
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var containerClient = CreateContainer(account, "test", "blob1");

        var batchClient = containerClient.GetBlobBatchClient();

        var act = () => batchClient.DeleteBlobs([containerClient.GetBlobClient("blob1").Uri], snapshotsOption);

        act.Should().Throw<NotSupportedException>();
    }

    [TestMethod]
    public void Unsupported_Methods_Should_Result_In_Not_Supported_Exception()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var batchClient = InMemoryBlobBatchClient.FromAccount(account);

        var createBatch = () => batchClient.CreateBatch();
        var submitBatch = () => batchClient.SubmitBatch(null!);
        Action submitBatchAsync = () => batchClient.SubmitBatchAsync(null!);
        var setBlobsAccessTier = () => batchClient.SetBlobsAccessTier([], AccessTier.Cool);
        Action setBlobsAccessTierAsync = () => batchClient.SetBlobsAccessTierAsync([], AccessTier.Cool);

        createBatch.Should().Throw<NotSupportedException>();
        submitBatch.Should().Throw<NotSupportedException>();
        submitBatchAsync.Should().Throw<NotSupportedException>();
        setBlobsAccessTier.Should().Throw<NotSupportedException>();
        setBlobsAccessTierAsync.Should().Throw<NotSupportedException>();
    }

    private static RequestFailedException ShouldThrowSingleSubRequestFailure(Func<Response[]> act)
    {
        var exception = act.Should().Throw<AggregateException>().Which;

        return exception.InnerExceptions
            .Should().ContainSingle()
            .Which.Should().BeOfType<RequestFailedException>()
            .Subject;
    }

    private static IReadOnlyList<BlobItem> GetBlobs(InMemoryBlobContainerClient containerClient)
    {
        return containerClient
            .GetBlobs(traits: BlobTraits.None, states: BlobStates.None, prefix: null, cancellationToken: default)
            .ToList();
    }

    private static InMemoryBlobContainerClient CreateContainer(InMemoryStorageAccount account, string containerName, params string[] blobNames)
    {
        var containerClient = InMemoryBlobContainerClient.FromAccount(account, containerName);

        containerClient.Create();

        foreach (var blobName in blobNames)
        {
            containerClient.UploadBlob(blobName, BinaryData.FromString("test"));
        }

        return containerClient;
    }
}
