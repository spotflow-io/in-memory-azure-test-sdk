using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;
using Spotflow.InMemory.Azure.Storage.Resources;

namespace Tests.Storage.Blobs;

[TestClass]
public class BlobServiceClientTests
{
    [TestMethod]
    public void Constructor_With_Connection_String_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var connectionString = account.CreateConnectionString();

        var client = new InMemoryBlobServiceClient(connectionString, provider);

        AssertClientProperties(client, account);
    }

    [TestMethod]
    public void Constructor_With_Sas_Uri_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var client = new InMemoryBlobServiceClient(account.BlobServiceUri, provider);

        AssertClientProperties(client, account);
    }

    [TestMethod]
    public void Construct_From_Account_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var client = InMemoryBlobServiceClient.FromAccount(account);

        AssertClientProperties(client, account);
    }

    private static void AssertClientProperties(InMemoryBlobServiceClient client, InMemoryStorageAccount account)
    {
        client.AccountName.Should().Be(account.Name);
        client.Uri.Should().Be(account.BlobServiceUri);
        client.CanGenerateAccountSasUri.Should().BeFalse();
    }
}
