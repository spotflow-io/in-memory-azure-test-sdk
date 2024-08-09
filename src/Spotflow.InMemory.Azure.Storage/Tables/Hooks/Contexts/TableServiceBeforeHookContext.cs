using Spotflow.InMemory.Azure.Storage.Hooks;
using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public abstract class TableServiceBeforeHookContext(StorageAccountScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : StorageBeforeHookContext(scope, provider, cancellationToken)
{
    public override TableServiceFaultsBuilder Faults() => new(this);
}
