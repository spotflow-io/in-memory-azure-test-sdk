using Azure;
using Azure.Data.Tables;

using Microsoft.Extensions.Time.Testing;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Resources;
using Spotflow.InMemory.Azure.Storage.Tables;

using Tests.Utils;

namespace Tests.Storage.Tables;

[TestClass]
public class TableClientTests
{
    private class CustomEntity : ITableEntity
    {
        public required string PartitionKey { get; set; }
        public required string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public DateTimeOffset? CustomProperty2 { get; set; }
        public long? CustomProperty3 { get; set; }
        public int CustomProperty1 { get; set; }
    }

    [TestMethod]
    public void Constructor_With_Connection_String_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var connectionString = account.CreateConnectionString();

        var client = new InMemoryTableClient(connectionString, "test", provider);

        AssertClientProperties(client, "test", account);
    }

    [TestMethod]
    public void Constructor_With_Uri_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var client = new InMemoryTableClient(account.CreateTableSasUri("test"), provider);

        AssertClientProperties(client, "test", account);
    }

    [TestMethod]
    public void Constructor_With_Uri_Without_Table_Should_Fail()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var act = () => new InMemoryTableClient(account.TableServiceUri, provider);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Table name must be specified when creating a table client.");
    }

    [TestMethod]
    public void Construct_From_Account_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var client = InMemoryTableClient.FromAccount(account, "test");

        AssertClientProperties(client, "test", account);
    }

    private static void AssertClientProperties(InMemoryTableClient client, string expectedTableName, InMemoryStorageAccount account)
    {
        var expectedUri = new Uri(account.TableServiceUri, expectedTableName);

        client.Uri.Should().Be(expectedUri);
        client.AccountName.Should().Be(account.Name);
        client.Name.Should().Be(expectedTableName);
    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void GetEntity_For_Non_Existing_Entity_Should_Fail()
    {
        var tableClient = ImplementationProvider.GetTableClient();

        var partitionKey = Guid.NewGuid().ToString();

        tableClient.CreateIfNotExists();

        var act = () => tableClient.GetEntity<TableEntity>(partitionKey, "rk");

        var exception = act.Should().Throw<RequestFailedException>().Which;

        exception.Status.Should().Be(404);
        exception.ErrorCode.Should().Be("ResourceNotFound");

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void AddEntity_Should_Create_Entity()
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var entity = new TableEntity(partitionKey, "rk") { ["TestProperty"] = 42 };

        tableClient.AddEntity(entity);

        var fetchedEntity = tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value;

        fetchedEntity.GetInt32("TestProperty").Should().Be(42);
    }

    [TestMethod]
    public void AddEntity_Should_Create_Entity_And_Set_Timestamp()
    {
        var timeProvider = new FakeTimeProvider();

        var account = new InMemoryStorageProvider(timeProvider: timeProvider).AddAccount();

        var tableClient = InMemoryTableClient.FromAccount(account, "TestTable");

        tableClient.Create();

        var entity = new TableEntity("pk", "rk") { ["TestProperty"] = 42 };

        tableClient.AddEntity(entity);

        var fetchedEntity = tableClient.GetEntity<TableEntity>("pk", "rk").Value;

        fetchedEntity.GetInt32("TestProperty").Should().Be(42);
        fetchedEntity.ETag.ToString().Should().NotBeNullOrWhiteSpace();
        fetchedEntity.Timestamp.Should().Be(timeProvider.GetUtcNow());
    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void AddEntity_For_Existing_Entity_Should_Fail()
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var entity = new TableEntity(partitionKey, "rk");

        tableClient.AddEntity(entity);

        var act = () => tableClient.AddEntity(entity);

        var exception = act.Should().Throw<RequestFailedException>().Which;

        exception.Status.Should().Be(409);
        exception.ErrorCode.Should().Be("EntityAlreadyExists");

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Delete_Existing_Entity_Should_Succeed()
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var entity = new TableEntity(partitionKey, "rk");

        tableClient.AddEntity(entity);

        tableClient.Query<TableEntity>(e => e.PartitionKey == partitionKey).Should().ContainSingle(e => e.RowKey == "rk");

        tableClient.DeleteEntity(partitionKey, "rk");

        tableClient.Query<TableEntity>(e => e.PartitionKey == partitionKey).Should().BeEmpty();

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Delete_Missing_Entity_Without_ETag_Should_Succeed()
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        tableClient.DeleteEntity(partitionKey, "rk");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Delete_Missing_Entity_With_ETag_Should_Not_Fail()
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var eTag = new ETag("W/\"datetime'2024-06-19T14%3A29%3A09.5839429Z'\"");

        var response = tableClient.DeleteEntity(partitionKey, "rk", ifMatch: eTag);

        response.Status.Should().Be(404);

    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Upsert_Existing_Entity_With_Merge_Should_Succeed()
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var entity1 = new TableEntity(partitionKey, "rk") { ["TestProperty1"] = 11, ["TestProperty2"] = 12 };
        var entity2 = new TableEntity(partitionKey, "rk") { ["TestProperty1"] = 21, ["TestProperty3"] = 23 };

        tableClient.UpsertEntity(entity1, TableUpdateMode.Merge);
        tableClient.UpsertEntity(entity2, TableUpdateMode.Merge);

        var fetchedEntity = tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value;

        fetchedEntity.GetInt32("TestProperty1").Should().Be(21);
        fetchedEntity.GetInt32("TestProperty2").Should().Be(12);
        fetchedEntity.GetInt32("TestProperty3").Should().Be(23);

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Upsert_Existing_Entity_With_Replace_ShouldSucceed()
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var entity1 = new TableEntity(partitionKey, "rk") { ["TestProperty1"] = 11, ["TestProperty2"] = 12 };
        var entity2 = new TableEntity(partitionKey, "rk") { ["TestProperty1"] = 21, ["TestProperty3"] = 23 };

        tableClient.UpsertEntity(entity1, TableUpdateMode.Replace);
        tableClient.UpsertEntity(entity2, TableUpdateMode.Replace);

        var fetchedEntity = tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value;

        fetchedEntity.GetInt32("TestProperty1").Should().Be(21);
        fetchedEntity.GetInt32("TestProperty2").Should().BeNull();
        fetchedEntity.GetInt32("TestProperty3").Should().Be(23);

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(TableUpdateMode.Replace, DisplayName = "Replace")]
    [DataRow(TableUpdateMode.Merge, DisplayName = "Merge")]
    public void Upsert_New_Entity_Should_Succeed(TableUpdateMode updateMode)
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var entity = new TableEntity(partitionKey, "rk") { ["TestProperty"] = 42 };

        tableClient.UpsertEntity(entity, updateMode);

        var fetchedEntity = tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value;

        fetchedEntity.ETag.ToString().Should().NotBeNullOrWhiteSpace();
        fetchedEntity.GetInt32("TestProperty").Should().Be(42);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(TableUpdateMode.Replace, DisplayName = "Replace")]
    [DataRow(TableUpdateMode.Merge, DisplayName = "Merge")]
    public void UpsertEntity_Of_Custom_Type_Should_Succeed(TableUpdateMode updateMode)
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var now = DateTimeOffset.UtcNow;

        var partitionKey = Guid.NewGuid().ToString();

        var entity = new CustomEntity
        {
            PartitionKey = partitionKey,
            RowKey = "rk",
            CustomProperty1 = 42,
            CustomProperty2 = now,
            CustomProperty3 = 4242
        };

        tableClient.UpsertEntity(entity, updateMode);

        var fetchedEntity = tableClient.GetEntity<CustomEntity>(partitionKey, "rk").Value;

        fetchedEntity.ETag.ToString().Should().NotBeNullOrWhiteSpace();
        fetchedEntity.CustomProperty1.Should().Be(42);
        fetchedEntity.CustomProperty2.Should().Be(now);
        fetchedEntity.CustomProperty3.Should().Be(4242);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(TableUpdateMode.Replace, DisplayName = "Replace")]
    [DataRow(TableUpdateMode.Merge, DisplayName = "Merge")]
    public void UpsertEntity_Should_Change_ETag(TableUpdateMode updateMode)
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var entity1 = new TableEntity(partitionKey, "rk") { ["TestProperty"] = 42 };

        tableClient.AddEntity(entity1);

        var entity2 = tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value;

        entity2["TestProperty"] = 43;

        tableClient.UpsertEntity(entity2, updateMode);

        var entity3 = tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value;

        entity3.GetInt32("TestProperty").Should().Be(43);
        entity3.ETag.Should().NotBe(entity2.ETag);

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(TableUpdateMode.Replace, DisplayName = "Replace")]
    [DataRow(TableUpdateMode.Merge, DisplayName = "Merge")]
    public void UpsertEntity_For_Existing_Entity_Without_ETag_Should_Succeeed(TableUpdateMode updateMode)
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var entity1 = new TableEntity(partitionKey, "rk");
        var entity2 = new TableEntity(partitionKey, "rk");

        tableClient.UpsertEntity(entity1, updateMode);
        tableClient.UpsertEntity(entity2, updateMode);

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(TableUpdateMode.Replace, DisplayName = "Replace")]
    [DataRow(TableUpdateMode.Merge, DisplayName = "Merge")]
    public void UpsertEntity_For_Existing_Entity_With_Different_ETag_Should_Succeed(TableUpdateMode updateMode)
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var eTag = new ETag("W/\"datetime'2024-06-19T14%3A29%3A09.5839429Z'\"");

        var entity1 = new TableEntity(partitionKey, "rk");
        var entity2 = new TableEntity(partitionKey, "rk") { ETag = eTag, ["test"] = 42 };

        var act1 = () => tableClient.UpsertEntity(entity1, updateMode);

        act1.Should().NotThrow();

        var act2 = () => tableClient.UpsertEntity(entity2, updateMode);  // ETag should be ignored by the service.

        act2.Should().NotThrow();

        tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value.GetInt32("test").Should().Be(42);

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(TableUpdateMode.Replace, DisplayName = "Replace")]
    [DataRow(TableUpdateMode.Merge, DisplayName = "Merge")]
    public void UpsertEntity_For_Missing_Entity_With_ETag_Should_Succeed(TableUpdateMode updateMode)
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var eTag = new ETag("W/\"datetime'2024-06-19T14%3A29%3A09.5839429Z'\"");

        var entity = new TableEntity(partitionKey, "rk") { ETag = eTag };

        var act = () => tableClient.UpsertEntity(entity, updateMode); // ETag should be ignored by the service.

        act.Should().NotThrow();

        tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value.Should().NotBeNull();

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(TableUpdateMode.Replace, DisplayName = "Replace")]
    [DataRow(TableUpdateMode.Merge, DisplayName = "Merge")]
    public void UpdateEntity_For_Existing_Entity_Should_Succeed(TableUpdateMode updateMode)
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var entity = new TableEntity(partitionKey, "rk") { ["property1"] = 41 };

        var addResponse = tableClient.AddEntity(entity);

        var eTagBeforeUpdate = addResponse.Headers.ETag;

        eTagBeforeUpdate.Should().NotBeNull();

        entity["property1"] = 42;

        var updateResponse = tableClient.UpdateEntity(entity, eTagBeforeUpdate!.Value, updateMode);

        var eTagAfterUpdate = updateResponse.Headers.ETag;

        eTagAfterUpdate.Should().NotBeNull();
        eTagAfterUpdate.Should().NotBe(eTagBeforeUpdate);

        var fetchedEntity = tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value;

        fetchedEntity.GetInt32("property1").Should().Be(42);

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(TableUpdateMode.Replace, DisplayName = "Replace")]
    [DataRow(TableUpdateMode.Merge, DisplayName = "Merge")]
    public void UpdateEntity_For_Missing_Entity_Should_Fail(TableUpdateMode updateMode)
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var partitionKey = Guid.NewGuid().ToString();

        var eTag = new ETag("W/\"datetime'2024-06-19T14%3A29%3A09.5839429Z'\"");

        var entity = new TableEntity(partitionKey, "rk") { ETag = eTag };

        var act = () => tableClient.UpdateEntity(entity, entity.ETag, updateMode);

        var exception = act.Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 404)
            .Where(e => e.ErrorCode == "ResourceNotFound");
    }
}
