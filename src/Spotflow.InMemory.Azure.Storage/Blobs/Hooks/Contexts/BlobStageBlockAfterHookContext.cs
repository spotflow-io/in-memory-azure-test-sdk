using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobStageBlockAfterHookContext : BlobAfterHookContext
{
    internal BlobStageBlockAfterHookContext(BlobStageBlockBeforeHookContext beforeContext, BlockInfo blockInfo) : base(beforeContext)
    {
        BeforeContext = beforeContext;
        BlockInfo = blockInfo;
    }

    public BlobStageBlockBeforeHookContext BeforeContext { get; }
    public BlockInfo BlockInfo { get; }
}
