using Azure.Data.Tables;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Resources;
using Spotflow.InMemory.Azure.Storage.Tables;

namespace Tests.Storage.Tables;

[TestClass]
public class TableServiceClientTests
{
    [TestMethod]
    public void Constructor_With_Connection_String_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var connectionString = account.CreateConnectionString();

        var client = new InMemoryTableServiceClient(connectionString, provider);

        AssertClientProperties(client, account);
    }

    [TestMethod]
    public void Constructor_With_Uri_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var client = new InMemoryTableServiceClient(account.TableServiceUri, provider);

        AssertClientProperties(client, account);
    }

    [TestMethod]
    public void Construct_From_Account_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var client = InMemoryTableServiceClient.FromAccount(account);

        AssertClientProperties(client, account);
    }

    [TestMethod]
    public void Service_Uri_Should_End_With_Slash()
    {
        var connectionString = "AccountName=test1;AccountKey=dGVzdHRlc3Q=;";
        var provider = new InMemoryStorageProvider();

        var realTableServiceClient = new TableServiceClient(connectionString);
        realTableServiceClient.Uri.ToString().Should().Be("https://test1.table.core.windows.net/");

        var inMemoryTableServiceClient = new InMemoryTableServiceClient(connectionString, provider);
        inMemoryTableServiceClient.Uri.ToString().Should().Be("https://test1.table.storage.in-memory.example.com/");

    }

    private static void AssertClientProperties(InMemoryTableServiceClient client, InMemoryStorageAccount account)
    {
        client.Uri.Should().Be(account.TableServiceUri);
        client.AccountName.Should().Be(account.Name);
    }
}
