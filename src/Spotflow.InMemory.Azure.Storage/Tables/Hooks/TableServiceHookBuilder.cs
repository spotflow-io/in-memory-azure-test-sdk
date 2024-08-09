using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.Storage.Hooks;
using Spotflow.InMemory.Azure.Storage.Hooks.Internals;
using Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;
using Spotflow.InMemory.Azure.Storage.Tables.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks;

public class TableServiceHookBuilder
{
    private readonly TableHookFilter _filter;

    internal TableServiceHookBuilder(StorageHookFilter filter)
    {
        _filter = new(filter);
    }

    public TableOperationsBuilder ForTableOperations(string? tableName = null) => new(_filter.With(tableName: tableName));
    public EntityOperationsBuilder ForEntityOperations(string? tableName = null, string? partitionKey = null, string? rowKey = null)
    {
        return new(_filter.With(tableName: tableName, partitionKey: partitionKey, rowKey: rowKey));
    }

    public StorageHook<TableServiceBeforeHookContext> Before(
        HookFunc<TableServiceBeforeHookContext> hookFunction,
        string? tableName = null,
        TableOperations? tableOperations = null,
        string? partitionKey = null,
        string? rowKey = null,
        EntityOperations? entityOperations = null)
    {
        return new(hookFunction, _filter.With(tableName, tableOperations, partitionKey, rowKey, entityOperations));
    }

    public StorageHook<TableServiceAfterHookContext> After(
        HookFunc<TableServiceAfterHookContext> hookFunction,
        string? tableName = null,
        TableOperations? tableOperations = null,
        string? partitionKey = null,
        string? rowKey = null,
        EntityOperations? entityOperations = null)
    {
        return new(hookFunction, _filter.With(tableName, tableOperations, partitionKey, rowKey, entityOperations));
    }

    public class TableOperationsBuilder
    {
        private readonly TableHookFilter _filter;

        internal TableOperationsBuilder(TableHookFilter filter)
        {
            _filter = filter.With(entityOperations: EntityOperations.None);
        }

        public StorageHook<TableBeforeHookContext> Before(HookFunc<TableBeforeHookContext> hook, TableOperations? operations = null) => new(hook, _filter.With(tableOperations: operations));
        public StorageHook<TableAfterHookContext> After(HookFunc<TableAfterHookContext> hook, TableOperations? operations = null) => new(hook, _filter.With(tableOperations: operations));

        public StorageHook<TableCreateBeforeHookContext> BeforeCreate(HookFunc<TableCreateBeforeHookContext> hook) => new(hook, _filter);

        public StorageHook<TableCreateAfterHookContext> AfterCreate(HookFunc<TableCreateAfterHookContext> hook) => new(hook, _filter);

        public StorageHook<TableQueryBeforeHookContext> BeforeQuery(HookFunc<TableQueryBeforeHookContext> hook) => new(hook, _filter);

        public StorageHook<TableQueryAfterHookContext> AfterQuery(HookFunc<TableQueryAfterHookContext> hook) => new(hook, _filter);
    }


    public class EntityOperationsBuilder
    {
        private readonly TableHookFilter _filter;

        internal EntityOperationsBuilder(TableHookFilter filter)
        {
            _filter = filter.With(tableOperations: TableOperations.None);
        }

        public StorageHook<EntityBeforeHookContext> Before(HookFunc<EntityBeforeHookContext> hook, EntityOperations? operations = null) => new(hook, _filter.With(entityOperations: operations));

        public StorageHook<EntityAfterHookContext> After(HookFunc<EntityAfterHookContext> hook, EntityOperations? operations = null) => new(hook, _filter.With(entityOperations: operations));

        public StorageHook<EntityAddBeforeHookContext> BeforeAdd(HookFunc<EntityAddBeforeHookContext> hook) => new(hook, _filter);

        public StorageHook<EntityAddAfterHookContext> AfterAdd(HookFunc<EntityAddAfterHookContext> hook) => new(hook, _filter);

        public StorageHook<EntityUpsertBeforeHookContext> BeforeUpsert(HookFunc<EntityUpsertBeforeHookContext> hook) => new(hook, _filter);

        public StorageHook<EntityUpsertAfterHookContext> AfterUpsert(HookFunc<EntityUpsertAfterHookContext> hook) => new(hook, _filter);
    }




}
