using Spotflow.InMemory.Azure.Storage.Tables.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public abstract class EntityAfterHookContext(EntityBeforeHookContext before) : TableServiceAfterHookContext(before), IEntityOperation
{
    public EntityOperations Operation => before.Operation;

    public string TableName => before.TableName;

    public string PartitionKey => before.PartitionKey;

    public string RowKey => before.RowKey;
}
