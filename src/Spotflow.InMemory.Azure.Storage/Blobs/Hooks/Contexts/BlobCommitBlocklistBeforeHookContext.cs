using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobCommitBlockListBeforeHookContext(BlobScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : BlobBeforeHookContext(scope, BlobOperations.CommitBlockList, provider, cancellationToken)
{
    public required CommitBlockListOptions? Options { get; init; }
}
