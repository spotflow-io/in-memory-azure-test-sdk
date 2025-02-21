using Azure;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

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

        AssertClientProperties(client, "test-container", "test-blob", account, canGenerateSasUri: true);
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

    [TestMethod]
    public void Construct_From_Account_With_Connection_String_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var client = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob", useConnectionString: true);

        AssertClientProperties(client, "test-container", "test-blob", account, canGenerateSasUri: true);
    }

    private static void AssertClientProperties(
        InMemoryBlobClient client,
        string expectedContainerName,
        string expectedBlobName,
        InMemoryStorageAccount account,
        bool canGenerateSasUri = false)
    {
        var expectedUri = new Uri(account.BlobServiceUri, $"{expectedContainerName}/{expectedBlobName}");

        client.Uri.Should().Be(expectedUri);
        client.AccountName.Should().Be(account.Name);
        client.BlobContainerName.Should().Be(expectedContainerName);
        client.Name.Should().Be(expectedBlobName);
        client.CanGenerateSasUri.Should().Be(canGenerateSasUri);
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

    [TestMethod]
    public void GenerateSasUri_With_Manually_Created_Client_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount("testaccount");

        var connectionString = account.GetConnectionString();

        var client = new InMemoryBlobClient(connectionString, "test-container", "test-blob", provider);

        var sasUri = client.GenerateSasUri(BlobSasPermissions.Read, new DateTimeOffset(2025, 01, 03, 17, 46, 00, TimeSpan.Zero));

        var expectedUri = $"https://testaccount.blob.storage.in-memory.example.com/test-container/test-blob?sv=2024-05-04&se=2025-01-03T17%3A46%3A00Z&sr=b&sp=r&sig=*";

        sasUri.ToString().Should().Match(expectedUri);
    }

    [TestMethod]
    public void GenerateSasUri_With_Client_Created_From_Container_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount("testaccount");

        var container = InMemoryBlobContainerClient.FromAccount(account, "test-container", useConnectionString: true);

        var client = container.GetBlobClient("test-blob");

        var sasUri = client.GenerateSasUri(BlobSasPermissions.Read, new DateTimeOffset(2025, 01, 03, 17, 46, 00, TimeSpan.Zero));

        var expectedUri = $"https://testaccount.blob.storage.in-memory.example.com/test-container/test-blob?sv=2024-05-04&se=2025-01-03T17%3A46%3A00Z&sr=b&sp=r&sig=*";

        sasUri.ToString().Should().Match(expectedUri);
    }
}
