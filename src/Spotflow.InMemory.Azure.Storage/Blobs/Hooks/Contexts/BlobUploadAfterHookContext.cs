using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobUploadAfterHookContext(BlobUploadBeforeHookContext beforeContext) : BlobAfterHookContext(beforeContext)
{
    public required BinaryData Content { get; init; }
    public required BlobContentInfo BlobContentInfo { get; init; }
    public BlobUploadBeforeHookContext beforeContext { get; } = beforeContext;
}
