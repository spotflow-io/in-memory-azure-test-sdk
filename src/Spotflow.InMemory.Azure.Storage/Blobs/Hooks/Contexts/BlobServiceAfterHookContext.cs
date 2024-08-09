using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public abstract class BlobServiceAfterHookContext(BlobServiceBeforeHookContext before) : StorageAfterHookContext(before)
{
    public override BlobServiceFaultsBuilder Faults() => new(this);
}
