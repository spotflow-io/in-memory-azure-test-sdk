using Spotflow.InMemory.Azure.Storage;

namespace Tests.Storage;

[TestClass]
public class StorageAccountTests
{
    [TestMethod]
    public void Connection_String_Should_Be_Properly_Formatted()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var connectionString = account.GetConnectionString();

        connectionString
            .Should()
            .Be($"AccountName={account.Name};AccountKey={account.PrimaryAccessKey};DefaultEndpointsProtocol=https;TableEndpoint={account.TableServiceUri};BlobEndpoint={account.BlobServiceUri}");
    }


    [TestMethod]
    public void Account_Should_Be_Case_Insensitive()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount("TestAccount");

        provider.GetAccount("testaccount").Should().BeSameAs(account);
        provider.GetAccount("TESTACCOUNT").Should().BeSameAs(account);

    }

    [TestMethod]
    public void Service_Uris_Should_End_With_Slash()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount("test1");

        account.BlobServiceUri.ToString().Should().Be("https://test1.blob.storage.in-memory.example.com/");
        account.TableServiceUri.ToString().Should().Be("https://test1.table.storage.in-memory.example.com/");
    }

}
