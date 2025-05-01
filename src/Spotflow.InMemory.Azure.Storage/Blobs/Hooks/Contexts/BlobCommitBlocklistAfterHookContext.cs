using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobCommitBlockListAfterHookContext : BlobAfterHookContext
{
    internal BlobCommitBlockListAfterHookContext(BlobCommitBlockListBeforeHookContext beforeContext, BlobContentInfo blobContentInfo) : base(beforeContext)
    {
        BeforeContext = beforeContext;
        BlobContentInfo = blobContentInfo;
    }

    public BlobCommitBlockListBeforeHookContext BeforeContext { get; }
    public BlobContentInfo BlobContentInfo { get; }
}
