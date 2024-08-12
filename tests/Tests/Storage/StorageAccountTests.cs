using Spotflow.InMemory.Azure.Storage;

namespace Tests.Storage;

[TestClass]
public class StorageAccountTests
{
    [TestMethod]
    public void Connection_String_Should_Be_Properly_Formatted()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var connectionString = account.CreateConnectionString();

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

}
