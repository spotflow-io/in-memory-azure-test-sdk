using Azure.Data.Tables;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public class EntityUpsertBeforeHookContext(EntityScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : EntityBeforeHookContext(scope, EntityOperations.Upsert, provider, cancellationToken)
{
    public required ITableEntity Entity { get; init; }
}
