using Azure;
using Azure.Storage.Blobs.Specialized;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;
using Spotflow.InMemory.Azure.Storage.Resources;

using Tests.Utils;

namespace Tests.Storage.Blobs;

[TestClass]
public class BlobClientTests_GenericBlobClient
{
    [TestMethod]
    public void Constructor_With_Connection_String_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var connectionString = account.GetConnectionString();

        var client = new InMemoryBlobClient(connectionString, "test-container", "test-blob", provider);

        AssertClientProperties(client, "test-container", "test-blob", account);
    }

    [TestMethod]
    public void Constructor_With_Uri_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var client = new InMemoryBlobClient(account.GetBlobSasUri("test-container", "test-blob"), provider);

        AssertClientProperties(client, "test-container", "test-blob", account);
    }

    [TestMethod]
    public void Constructor_With_Uri_Without_Container_Should_Fail()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var act = () => new InMemoryBlobClient(account.BlobServiceUri, provider);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Blob container name must be specified when creating a blob client.");
    }

    [TestMethod]
    public void Constructor_With_Uri_Without_Blob_Should_Fail()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var act = () => new InMemoryBlobClient(account.GetBlobContainerSasUri("test"), provider);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Blob name must be specified when creating a blob client.");
    }


    [TestMethod]
    public void Construct_From_Account_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var client = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        AssertClientProperties(client, "test-container", "test-blob", account);
    }

    private static void AssertClientProperties(
        InMemoryBlobClient client,
        string expectedContainerName,
        string expectedBlobName,
        InMemoryStorageAccount account)
    {
        var expectedUri = new Uri(account.BlobServiceUri, $"{expectedContainerName}/{expectedBlobName}");

        client.Uri.Should().Be(expectedUri);
        client.AccountName.Should().Be(account.Name);
        client.BlobContainerName.Should().Be(expectedContainerName);
        client.Name.Should().Be(expectedBlobName);
        client.CanGenerateSasUri.Should().BeFalse();
    }

    [TestMethod]
    public void GetParentBlobContainerClient_Should_Return_Working_Client()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test-container");

        containerClient.Create();

        var blobClient = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blobClient.Upload(BinaryData.FromString("test-data"));

        var containerClientFromBlob = blobClient.GetParentBlobContainerClient();

        var blobs = containerClientFromBlob.GetBlobs().ToList();

        blobs.Should().ContainSingle(blob => blob.Name == "test-blob");

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Upload_For_Existing_Blob_Without_Overwrite_Flag_Should_Fail()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobClient(blobName);

        var data = BinaryData.FromString("test-data");

        var act = () => blobClient.Upload(data, overwrite: false);

        act.Should().NotThrow();

        act.Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 409)
            .Where(e => e.ErrorCode == "BlobAlreadyExists");
    }
}
