using Azure;
using Azure.Data.Tables;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Tables;

using Tests.Utils;

namespace Tests.Storage.Tables;

[TestClass]
public class TransactionTests
{
    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Transaction_With_Mixed_Actions_Should_Succeed()
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var primaryKey = Guid.NewGuid().ToString();

        tableClient.AddEntity(new TableEntity(primaryKey, "rk-1"));
        tableClient.AddEntity(new TableEntity(primaryKey, "rk-2"));
        tableClient.AddEntity(new TableEntity(primaryKey, "rk-3"));


        var transaction = new TableTransactionAction[]
        {
            new(TableTransactionActionType.Add, new TableEntity(primaryKey, "rk-4")),
            new(TableTransactionActionType.Delete, new TableEntity(primaryKey, "rk-1")),
            new(TableTransactionActionType.UpsertReplace, new TableEntity(primaryKey, "rk-5")),
            new(TableTransactionActionType.UpsertMerge, new TableEntity(primaryKey, "rk-6")),
            new(TableTransactionActionType.UpdateReplace, new TableEntity(primaryKey, "rk-2")) ,
            new(TableTransactionActionType.UpdateMerge, new TableEntity(primaryKey, "rk-3")),
        };

        tableClient.SubmitTransaction(transaction);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(TableTransactionActionType.UpsertReplace, DisplayName = "UpsertReplace")]
    [DataRow(TableTransactionActionType.UpsertMerge, DisplayName = "UpsertMerge")]
    public void Transaction_Upsert_With_Non_Matching_ETag_Should_Succeed(TableTransactionActionType actionType)
    {
        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        var tableClientWithTestTimeChecks = InMemoryTableClient.FromAccount(new InMemoryStorageProvider(disableTestTimeChecks: false).AddAccount(), "test");

        tableClientWithTestTimeChecks.Create();

        var primaryKey = Guid.NewGuid().ToString();

        var entity1 = new TableEntity(primaryKey, "rk") { ["value"] = 1 };

        // Add an entity so the upsert targets an existing row with a known ETag.

        tableClient.AddEntity(entity1);
        tableClientWithTestTimeChecks.AddEntity(entity1);


        // Use a deliberately wrong ETag - Upsert should ignore it.
        // ETag should be ignored by the service
        // https://learn.microsoft.com/en-us/rest/api/storageservices/insert-or-replace-entity#remarks

        var staleETag = new ETag("W/\"datetime'2000-01-01T00%3A00%3A00.0000000Z'\"");

        var entity2 = new TableEntity(primaryKey, "rk") { ETag = staleETag, ["value"] = 2 };

        var transaction = new TableTransactionAction[]
        {
            new(actionType, entity2, staleETag),
        };

        tableClient.SubmitTransaction(transaction);
        tableClient.GetEntity<TableEntity>(primaryKey, "rk").Value.GetInt32("value").Should().Be(2);

        var action = () => tableClientWithTestTimeChecks.SubmitTransaction(transaction);

        action.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("TEST-TIME CHECK: ETag for Upsert transaction action is ignored by the table service so explicitly specified ETag is a probable bug.");

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(TableTransactionActionType.UpdateReplace, DisplayName = "UpdateReplace")]
    [DataRow(TableTransactionActionType.UpdateMerge, DisplayName = "UpdateMerge")]
    public void Transaction_Update_With_Correct_ETag_On_Action_But_Stale_ETag_On_Entity_Should_Succeed(TableTransactionActionType actionType)
    {
        var partitionKey = Guid.NewGuid().ToString();

        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        tableClient.AddEntity(new TableEntity(partitionKey, "rk") { ["value"] = 1 });

        var currentETag = tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value.ETag;

        var staleETag = new ETag("W/\"datetime'2000-01-01T00%3A00%3A00.0000000Z'\"");

        // Entity carries a stale ETag, but the action carries the correct one — action ETag takes precedence.
        var entity = new TableEntity(partitionKey, "rk") { ETag = staleETag, ["value"] = 2 };

        var transaction = new TableTransactionAction[]
        {
            new(actionType, entity, currentETag),
        };

        tableClient.SubmitTransaction(transaction);

        tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value.GetInt32("value").Should().Be(2);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(TableTransactionActionType.UpdateReplace, DisplayName = "UpdateReplace")]
    [DataRow(TableTransactionActionType.UpdateMerge, DisplayName = "UpdateMerge")]
    public void Transaction_Update_With_Stale_ETag_On_Action_But_Correct_ETag_On_Entity_Should_Fail(TableTransactionActionType actionType)
    {
        var partitionKey = Guid.NewGuid().ToString();

        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        tableClient.AddEntity(new TableEntity(partitionKey, "rk") { ["value"] = 1 });

        var currentETag = tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value.ETag;

        var staleETag = new ETag("W/\"datetime'2000-01-01T00%3A00%3A00.0000000Z'\"");

        // Entity carries the correct ETag, but the action carries a stale one — action ETag takes precedence.
        var entity = new TableEntity(partitionKey, "rk") { ETag = currentETag, ["value"] = 2 };

        var transaction = new TableTransactionAction[]
        {
            new(actionType, entity, staleETag),
        };

        var act = () => tableClient.SubmitTransaction(transaction);

        act.Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 412)
            .Where(e => e.ErrorCode == "UpdateConditionNotSatisfied");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(TableTransactionActionType.UpdateReplace, DisplayName = "UpdateReplace")]
    [DataRow(TableTransactionActionType.UpdateMerge, DisplayName = "UpdateMerge")]
    public void Transaction_Update_Without_Action_ETag_And_With_Stale_Entity_ETag_Should_Succeed(TableTransactionActionType actionType)
    {
        var partitionKey = Guid.NewGuid().ToString();

        var tableClient = ImplementationProvider.GetTableClient();

        tableClient.CreateIfNotExists();

        tableClient.AddEntity(new TableEntity(partitionKey, "rk") { ["value"] = 1 });

        var staleETag = new ETag("W/\"datetime'2000-01-01T00%3A00%3A00.0000000Z'\"");

        // No ETag on the action — entity ETag should still be ignored.
        var entity = new TableEntity(partitionKey, "rk") { ETag = staleETag, ["value"] = 2 };

        var transaction = new TableTransactionAction[]
        {
            new(actionType, entity),
        };

        tableClient.SubmitTransaction(transaction);

        tableClient.GetEntity<TableEntity>(partitionKey, "rk").Value.GetInt32("value").Should().Be(2);
    }
}
