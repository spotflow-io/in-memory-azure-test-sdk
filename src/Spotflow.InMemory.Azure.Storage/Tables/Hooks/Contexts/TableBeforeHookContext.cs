using Spotflow.InMemory.Azure.Storage.Tables.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public abstract class TableBeforeHookContext(TableScope scope, TableOperations operation, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : TableServiceBeforeHookContext(scope, provider, cancellationToken), ITableOperation
{
    public string TableName => scope.TableName;
    public TableOperations Operation => operation;
}
