using Azure.Data.Tables;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public class EntityAddBeforeHookContext(EntityScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : EntityBeforeHookContext(scope, EntityOperations.Add, provider, cancellationToken)
{
    public required ITableEntity Entity { get; init; }
}
