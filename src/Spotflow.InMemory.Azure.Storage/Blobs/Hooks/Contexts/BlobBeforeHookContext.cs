using Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public abstract class BlobBeforeHookContext(BlobScope scope, BlobOperations operation, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : BlobServiceBeforeHookContext(scope, provider, cancellationToken), IBlobOperation
{
    public string ContainerName => scope.ContainerName;
    public string BlobName => scope.BlobName;
    public BlobOperations Operation => operation;
}

