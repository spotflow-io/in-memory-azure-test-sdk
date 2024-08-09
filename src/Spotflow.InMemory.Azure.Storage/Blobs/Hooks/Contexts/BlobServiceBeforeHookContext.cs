using Spotflow.InMemory.Azure.Storage.Hooks;
using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public abstract class BlobServiceBeforeHookContext(StorageAccountScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : StorageBeforeHookContext(scope, provider, cancellationToken)
{
    public override BlobServiceFaultsBuilder Faults() => new(this);
}
