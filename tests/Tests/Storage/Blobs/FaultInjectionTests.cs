using System.Text.RegularExpressions;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;

using Tests.Utils;

namespace Tests.Storage.Blobs;

[TestClass]
public class FaultInjectionTests
{
    [TestMethod]
    public void Service_Is_Busy_Should_Throw_Exception()
    {
        var provider = new InMemoryStorageProvider();

        provider.AddHook(hook => hook.ForBlobService().Before(ctx => ctx.Faults().ServiceIsBusy()));

        var account = provider.AddAccount("test-account");

        var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test-container");

        var act = () => containerClient.Create();

        act.Should()
            .Throw<RequestFailedException>()
            .WithMessage("Blob service in account 'test-account' is busy.")
            .Where(ex => ex.Status == 503)
            .Where(ex => ex.ErrorCode == "ServerBusy");
    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Authentication_Failed_Should_Throw_Exception()
    {
        Action act;

        if (!ImplementationProvider.IsAzureConfigAvailable)
        {
            var provider = new InMemoryStorageProvider();

            provider.AddHook(hook => hook.ForBlobService().Before(ctx => ctx.Faults().AuthenticationFailedSignatureDidNotMatch()));

            var account = provider.AddAccount("test-account");

            var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test-container");

            act = () => containerClient.Create();
        }
        else
        {
            var connectionString = await ImplementationProvider.GetStorageConnectionString();

            var serviceClient = new BlobServiceClient(connectionString);
            var containerClient = serviceClient.GetBlobContainerClient(Guid.NewGuid().ToString());
            var blobClient = containerClient.GetBlobClient(Guid.NewGuid().ToString());

            var sasUri = blobClient.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.AddHours(1));

            var invalidSasUri = new Uri(Regex.Replace(sasUri.ToString(), "sig=[^&]([^&]+)", "sig=x$1")); // Change one letter of the signature.

            act = () => new BlobClient(invalidSasUri).DownloadContent();
        }

        act.Should()
            .Throw<RequestFailedException>()
            .WithMessage("Server failed to authenticate the request. Make sure the value of Authorization header is formed correctly including the signature.*")
            .Where(ex => ex.Status == 403)
            .Where(ex => ex.ErrorCode == "AuthenticationFailed")
            .Which.Data["AuthenticationErrorDetail"].Should().BeOfType<string>()
            .Which.Should().StartWith("Signature did not match.");
    }





}
