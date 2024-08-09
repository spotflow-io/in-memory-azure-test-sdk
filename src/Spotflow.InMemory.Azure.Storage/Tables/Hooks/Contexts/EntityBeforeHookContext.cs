using Spotflow.InMemory.Azure.Storage.Tables.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public abstract class EntityBeforeHookContext(EntityScope scope, EntityOperations operation, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : TableServiceBeforeHookContext(scope, provider, cancellationToken), IEntityOperation
{
    public EntityOperations Operation => operation;

    public string TableName => scope.TableName;

    public string PartitionKey => scope.PartitionKey;

    public string RowKey => scope.RowKey;
}
