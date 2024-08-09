using Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public abstract class BlobAfterHookContext(BlobBeforeHookContext before) : BlobServiceAfterHookContext(before), IBlobOperation
{
    public BlobOperations Operation => before.Operation;

    public string ContainerName => before.ContainerName;

    public string BlobName => before.BlobName;
}
