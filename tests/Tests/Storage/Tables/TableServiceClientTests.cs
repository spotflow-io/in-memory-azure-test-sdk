using Azure;
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

        var connectionString = account.GetConnectionString();

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

    [TestMethod]
    public async Task Create_Table_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var serviceClient = new InMemoryTableServiceClient(account.TableServiceUri, provider);

        await serviceClient.CreateTableAsync("table-1");
        serviceClient.CreateTable("table-2");

        await serviceClient.CreateTableIfNotExistsAsync("table-3");
        serviceClient.CreateTableIfNotExists("table-4");

        var table1Client = serviceClient.GetTableClient("table-1");
        var table2Client = serviceClient.GetTableClient("table-2");
        var table3Client = serviceClient.GetTableClient("table-3");
        var table4Client = serviceClient.GetTableClient("table-4");

        table1Client.Query<TableEntity>().Count().Should().Be(0);
        table2Client.Query<TableEntity>().Count().Should().Be(0);
        table3Client.Query<TableEntity>().Count().Should().Be(0);
        table4Client.Query<TableEntity>().Count().Should().Be(0);

        var act1 = () => serviceClient.CreateTableAsync("table-1");
        var act2 = () => serviceClient.CreateTable("table-2");
        var act3 = () => serviceClient.CreateTableIfNotExistsAsync("table-3");
        var act4 = () => serviceClient.CreateTableIfNotExists("table-4");

        await act1.Should().ThrowAsync<RequestFailedException>().WithMessage("Table 'table-1' already exists in account '*'.");
        act2.Should().Throw<RequestFailedException>().WithMessage("Table 'table-2' already exists in account '*'.");
        await act3.Should().NotThrowAsync();
        act4.Should().NotThrow();
    }

    [TestMethod]
    public async Task Delete_Table_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var serviceClient = new InMemoryTableServiceClient(account.TableServiceUri, provider);

        await serviceClient.CreateTableAsync("table-1");
        await serviceClient.CreateTableAsync("table-2");

        serviceClient.DeleteTable("table-1");
        await serviceClient.DeleteTableAsync("table-2");

        var act1 = () => serviceClient.DeleteTable("table-1");
        var act2 = () => serviceClient.DeleteTableAsync("table-2");

        act1.Should().Throw<RequestFailedException>().WithMessage("Table 'table-1' not found in account '*'.");
        await act2.Should().ThrowAsync<RequestFailedException>().WithMessage("Table 'table-2' not found in account '*'.");
    }

    private static void AssertClientProperties(InMemoryTableServiceClient client, InMemoryStorageAccount account)
    {
        client.Uri.Should().Be(account.TableServiceUri);
        client.AccountName.Should().Be(account.Name);
    }
}
