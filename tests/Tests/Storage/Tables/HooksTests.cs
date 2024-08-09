using Azure;
using Azure.Data.Tables;

using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Tables;
using Spotflow.InMemory.Azure.Storage.Tables.Hooks;
using Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

namespace Tests.Storage.Tables;


[TestClass]
public class HooksTests
{
    [TestMethod]
    public async Task Table_Create_Hooks_Should_Execute()
    {
        const string accountName = "test-account";
        const string tableName = "test-table";
        var provider = new InMemoryStorageProvider();

        TableCreateBeforeHookContext? capturedBeforeContext = null;
        TableCreateAfterHookContext? capturedAfterContext = null;

        provider.AddHook(builder => builder.ForTableService().ForTableOperations().BeforeCreate(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }));

        provider.AddHook(builder => builder.ForTableService().ForTableOperations().AfterCreate(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }));

        var account = provider.AddAccount(accountName);
        var tableClient = InMemoryTableClient.FromAccount(account, tableName);

        await tableClient.CreateAsync();

        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext?.StorageAccountName.Should().Be(accountName);
        capturedBeforeContext?.TableName.Should().BeEquivalentTo(tableName);
        capturedBeforeContext?.Operation.Should().Be(TableOperations.Create);

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.StorageAccountName.Should().Be(accountName);
        capturedAfterContext?.TableName.Should().Be(tableName);
        capturedAfterContext?.Operation.Should().Be(TableOperations.Create);
    }

    [TestMethod]
    public void Table_Query_Hook_Should_Execute()
    {
        var provider = new InMemoryStorageProvider();

        TableQueryBeforeHookContext? capturedBeforeContext = null;
        TableQueryAfterHookContext? capturedAfterContext = null;

        provider.AddHook(hook => hook.ForTableService().ForTableOperations().BeforeQuery(ctx => { capturedBeforeContext = ctx; return Task.CompletedTask; }));
        provider.AddHook(hook => hook.ForTableService().ForTableOperations().AfterQuery(ctx => { capturedAfterContext = ctx; return Task.CompletedTask; }));

        var account = provider.AddAccount("test-account");

        var tableClient = InMemoryTableClient.FromAccount(account, "test-table");

        tableClient.Create();

        tableClient.AddEntity(new TestEntity() { PartitionKey = "pk", RowKey = "rk" });

        tableClient.Query<TableEntity>().ToList().Should().HaveCount(1);

        capturedBeforeContext.Should().NotBeNull();
        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext!.Entities.Should().HaveCount(1);
    }

    [TestMethod]
    public async Task Hooks_With_Different_Scope_Should_Not_Execute()
    {
        const string accountName = "test-account";
        const string tableName = "test-table";
        var provider = new InMemoryStorageProvider();

        HookFunc<TableCreateBeforeHookContext> failingBeforeHook = _ => throw new InvalidOperationException("This hook should not execute.");
        HookFunc<TableCreateAfterHookContext> failingAfterHook = _ => throw new InvalidOperationException("This hook should not execute.");

        provider.AddHook(hook => hook.ForTableService("different-acc").ForTableOperations().BeforeCreate(failingBeforeHook));
        provider.AddHook(hook => hook.ForTableService("different-acc").ForTableOperations().BeforeCreate(failingBeforeHook));
        provider.AddHook(hook => hook.ForTableService().ForTableOperations(tableName: "different-table").BeforeCreate(failingBeforeHook));

        provider.AddHook(hook => hook.ForTableService("different-acc").ForTableOperations().AfterCreate(failingAfterHook));
        provider.AddHook(hook => hook.ForTableService().ForTableOperations(tableName: "different-table").AfterCreate(failingAfterHook));

        var account = provider.AddAccount(accountName);
        var tableClient = InMemoryTableClient.FromAccount(account, tableName);
        await tableClient.CreateAsync();
    }

    [TestMethod]
    public async Task Parent_Hook_Should_Execute()
    {
        const string accountName = "test-account";
        const string tableName = "test-table";
        var provider = new InMemoryStorageProvider();

        TableBeforeHookContext? capturedBeforeContext = null;
        TableAfterHookContext? capturedAfterContext = null;

        provider.AddHook(hook => hook.ForTableService().ForTableOperations().Before(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }));

        provider.AddHook(hook => hook.ForTableService().ForTableOperations().After(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }));

        var account = provider.AddAccount(accountName);
        var tableClient = InMemoryTableClient.FromAccount(account, tableName);

        await tableClient.CreateAsync();


        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext?.StorageAccountName.Should().BeEquivalentTo(accountName);
        capturedBeforeContext?.TableName.Should().Be(tableName);
        capturedBeforeContext?.Operation.Should().Be(TableOperations.Create);

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.StorageAccountName.Should().Be(accountName);
        capturedAfterContext?.TableName.Should().Be(tableName);
        capturedAfterContext?.Operation.Should().Be(TableOperations.Create);
    }

    [TestMethod]
    public async Task Targeted_Hooks__Should_Execute()
    {
        const string accountName = "test-account";
        const string tableName = "test-table";
        var provider = new InMemoryStorageProvider();

        TableBeforeHookContext? capturedBeforeContext = null;
        TableAfterHookContext? capturedAfterContext = null;

        provider.AddHook(hook => hook.ForTableService().ForTableOperations().Before(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }, TableOperations.Create));

        provider.AddHook(hook => hook.ForTableService().ForTableOperations().After(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }, TableOperations.Create));

        var account = provider.AddAccount(accountName);
        var tableClient = InMemoryTableClient.FromAccount(account, tableName);

        await tableClient.CreateAsync();

        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext?.StorageAccountName.Should().Be(accountName);
        capturedBeforeContext?.TableName.Should().Be(tableName);
        capturedBeforeContext?.Operation.Should().Be(TableOperations.Create);

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.StorageAccountName.Should().Be(accountName);
        capturedAfterContext?.TableName.Should().Be(tableName);
        capturedAfterContext?.Operation.Should().Be(TableOperations.Create);
    }

    [TestMethod]
    public async Task Entity_Hooks_Should_Execute()
    {
        const string accountName = "test-account";
        const string tableName = "test-table";
        var provider = new InMemoryStorageProvider();

        EntityBeforeHookContext? capturedBeforeContext = null;
        EntityAfterHookContext? capturedAfterContext = null;

        provider.AddHook(hook => hook.ForTableService().ForEntityOperations().Before(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }, EntityOperations.Add));

        provider.AddHook(hook => hook.ForTableService().ForEntityOperations().After(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }, EntityOperations.Add));

        var account = provider.AddAccount(accountName);
        var tableClient = InMemoryTableClient.FromAccount(account, tableName);
        await tableClient.CreateAsync();

        await tableClient.AddEntityAsync(new TestEntity() { PartitionKey = "pk", RowKey = "rk" });

        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext?.StorageAccountName.Should().Be(accountName);
        capturedBeforeContext?.TableName.Should().Be(tableName);
        capturedBeforeContext?.Operation.Should().Be(EntityOperations.Add);

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.StorageAccountName.Should().Be(accountName);
        capturedAfterContext?.TableName.Should().Be(tableName);
        capturedAfterContext?.Operation.Should().Be(EntityOperations.Add);
    }

    [TestMethod]
    public async Task Hooks_With_Different_Target_Should_Not_Execute()
    {
        const string accountName = "test-account";
        const string tableName = "test-table";
        var provider = new InMemoryStorageProvider();

        HookFunc<EntityBeforeHookContext> failingBeforeHook = _ => throw new InvalidOperationException("This hook should not execute.");
        HookFunc<EntityAfterHookContext> failingAfterHook = _ => throw new InvalidOperationException("This hook should not execute.");

        provider.AddHook(hook => hook.ForTableService().ForEntityOperations().Before(failingBeforeHook, EntityOperations.Upsert));
        provider.AddHook(hook => hook.ForTableService().ForEntityOperations().After(failingAfterHook, EntityOperations.Upsert));

        var account = provider.AddAccount(accountName);
        var tableClient = InMemoryTableClient.FromAccount(account, tableName);
        await tableClient.CreateAsync();

        await tableClient.AddEntityAsync(new TestEntity() { PartitionKey = "pk", RowKey = "rk" });
    }

    private class TestEntity : ITableEntity
    {
        public required string PartitionKey { get; set; }
        public required string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
