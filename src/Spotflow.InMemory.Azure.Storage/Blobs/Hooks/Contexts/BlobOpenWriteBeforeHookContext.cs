using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobOpenWriteBeforeHookContext(BlobScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : BlobBeforeHookContext(scope, BlobOperations.OpenWrite, provider, cancellationToken)
{
    public required BlobOpenWriteOptions? Options { get; init; }
}
