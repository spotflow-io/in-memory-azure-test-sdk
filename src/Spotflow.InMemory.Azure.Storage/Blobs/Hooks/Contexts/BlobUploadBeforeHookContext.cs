using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobUploadBeforeHookContext(BlobScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : BlobBeforeHookContext(scope, BlobOperations.Upload, provider, cancellationToken)
{
    public required BinaryData Content { get; init; }

    public required BlobUploadOptions? Options { get; init; }


}
