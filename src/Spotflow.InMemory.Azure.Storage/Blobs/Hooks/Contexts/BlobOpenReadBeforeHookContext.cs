using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobOpenReadBeforeHookContext(BlobScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : BlobBeforeHookContext(scope, BlobOperations.OpenRead, provider, cancellationToken)
{
    public required BlobOpenReadOptions? Options { get; init; }
}
