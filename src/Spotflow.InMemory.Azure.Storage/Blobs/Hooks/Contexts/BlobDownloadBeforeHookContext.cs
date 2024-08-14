using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobDownloadBeforeHookContext(BlobScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : BlobBeforeHookContext(scope, BlobOperations.Download, provider, cancellationToken)
{
    public required BlobDownloadOptions? Options { get; init; }
}
