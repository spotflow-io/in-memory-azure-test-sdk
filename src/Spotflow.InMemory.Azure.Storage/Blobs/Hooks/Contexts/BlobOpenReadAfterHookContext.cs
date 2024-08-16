using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobOpenReadAfterHookContext(BlobOpenReadBeforeHookContext before) : BlobAfterHookContext(before)
{
    public required BinaryData Content { get; init; }
    public required BlobProperties BlobProperties { get; init; }
    public BlobOpenReadBeforeHookContext BeforeContext => before;
}
