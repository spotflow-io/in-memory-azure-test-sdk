using Azure.Data.Tables;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Tables;

namespace Tests.Storage.Tables;

[TestClass]
public class TransactionTests
{
    [TestMethod]
    public void Transaction_With_Mixed_Actions_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var tableClient = InMemoryTableClient.FromAccount(account, "TestTable");

        tableClient.Create();

        tableClient.AddEntity(new TableEntity("pk", "rk-1"));
        tableClient.AddEntity(new TableEntity("pk", "rk-2"));
        tableClient.AddEntity(new TableEntity("pk", "rk-3"));


        var transaction = new TableTransactionAction[]
        {
            new(TableTransactionActionType.Add, new TableEntity("pk", "rk-4")),
            new(TableTransactionActionType.Delete, new TableEntity("pk", "rk-1")),
            new(TableTransactionActionType.UpsertReplace, new TableEntity("pk", "rk-5")),
            new(TableTransactionActionType.UpsertMerge, new TableEntity("pk", "rk-6")),
            new(TableTransactionActionType.UpdateReplace, new TableEntity("pk", "rk-2")) ,
            new(TableTransactionActionType.UpdateMerge, new TableEntity("pk", "rk-3")),
        };

        tableClient.SubmitTransaction(transaction);
    }
}
