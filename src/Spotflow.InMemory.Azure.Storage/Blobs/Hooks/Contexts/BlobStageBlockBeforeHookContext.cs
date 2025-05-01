using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobStageBlockBeforeHookContext(BlobScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : BlobBeforeHookContext(scope, BlobOperations.StageBlock, provider, cancellationToken)
{
    public required BlockBlobStageBlockOptions? Options { get; init; }
}
