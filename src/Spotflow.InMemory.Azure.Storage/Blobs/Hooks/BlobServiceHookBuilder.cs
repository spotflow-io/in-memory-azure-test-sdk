using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;
using Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Internals;
using Spotflow.InMemory.Azure.Storage.Hooks;
using Spotflow.InMemory.Azure.Storage.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks;

public class BlobServiceHookBuilder
{
    private readonly BlobHookFilter _filter;

    internal BlobServiceHookBuilder(StorageHookFilter filter)
    {
        _filter = new(filter);
    }

    public BlobOperationsBuilder ForBlobOperations(string? containerName = null, string? blobName = null) => new(_filter.With(containerName: containerName, blobName: blobName));
    public ContainerOperationsBuilder ForContainerOperations(string? containerName = null) => new(_filter.With(containerName: containerName));

    public StorageHook<BlobServiceBeforeHookContext> Before(
        HookFunc<BlobServiceBeforeHookContext> hook,
        string? containerName = null,
        ContainerOperations? containerOperations = null,
        string? blobName = null,
        BlobOperations? blobOperations = null)
    {
        return new(hook, _filter.With(containerName, containerOperations, blobName, blobOperations));
    }

    public StorageHook<BlobServiceAfterHookContext> After(
        HookFunc<BlobServiceAfterHookContext> hook,
        string? containerName = null,
        ContainerOperations? containerOperations = null,
        string? blobName = null,
        BlobOperations? blobOperations = null)
    {
        return new(hook, _filter.With(containerName, containerOperations, blobName, blobOperations));
    }

    public class ContainerOperationsBuilder
    {
        private readonly BlobHookFilter _filter;

        internal ContainerOperationsBuilder(BlobHookFilter filter)
        {
            _filter = filter.With(blobOperations: BlobOperations.None);
        }

        public StorageHook<ContainerBeforeHookContext> Before(HookFunc<ContainerBeforeHookContext> hook, ContainerOperations? operations = null) => new(hook, _filter.With(containerOperations: operations));

        public StorageHook<ContainerAfterHookContext> After(HookFunc<ContainerAfterHookContext> hook, ContainerOperations? operations = null) => new(hook, _filter.With(containerOperations: operations));

        public StorageHook<ContainerCreateBeforeHookContext> BeforeCreate(HookFunc<ContainerCreateBeforeHookContext> hook) => new(hook, _filter);

        public StorageHook<ContainerCreateAfterHookContext> AfterCreate(HookFunc<ContainerCreateAfterHookContext> hook) => new(hook, _filter);

    }


    public class BlobOperationsBuilder
    {
        private readonly BlobHookFilter _filter;

        internal BlobOperationsBuilder(BlobHookFilter filter)
        {
            _filter = filter.With(containerOperations: ContainerOperations.None);
        }
        public StorageHook<BlobBeforeHookContext> Before(HookFunc<BlobBeforeHookContext> hook, BlobOperations? operations = null) => new(hook, _filter.With(blobOperations: operations));

        public StorageHook<BlobAfterHookContext> After(HookFunc<BlobAfterHookContext> hook, BlobOperations? operations = null) => new(hook, _filter.With(blobOperations: operations));

        public StorageHook<BlobDownloadBeforeHookContext> BeforeDownload(HookFunc<BlobDownloadBeforeHookContext> hook) => new(hook, _filter);

        public StorageHook<BlobDownloadAfterHookContext> AfterDownload(HookFunc<BlobDownloadAfterHookContext> hook) => new(hook, _filter);

        public StorageHook<BlobOpenReadBeforeHookContext> BeforeOpenRead(HookFunc<BlobOpenReadBeforeHookContext> hook) => new(hook, _filter);

        public StorageHook<BlobOpenReadAfterHookContext> AfterOpenRead(HookFunc<BlobOpenReadAfterHookContext> hook) => new(hook, _filter);

        public StorageHook<BlobOpenWriteBeforeHookContext> BeforeOpenWrite(HookFunc<BlobOpenWriteBeforeHookContext> hook) => new(hook, _filter);

        public StorageHook<BlobOpenWriteAfterHookContext> AfterOpenWrite(HookFunc<BlobOpenWriteAfterHookContext> hook) => new(hook, _filter);

        public StorageHook<BlobUploadBeforeHookContext> BeforeBlobUpload(HookFunc<BlobUploadBeforeHookContext> hook) => new(hook, _filter);

        public StorageHook<BlobUploadAfterHookContext> AfterBlobUpload(HookFunc<BlobUploadAfterHookContext> hook) => new(hook, _filter);

        public StorageHook<BlobStageBlockBeforeHookContext> BeforeStageBlock(HookFunc<BlobStageBlockBeforeHookContext> hook) => new(hook, _filter);

        public StorageHook<BlobStageBlockAfterHookContext> AfterStageBlock(HookFunc<BlobStageBlockAfterHookContext> hook) => new(hook, _filter);

        public StorageHook<BlobCommitBlockListBeforeHookContext> BeforeCommitBlockList(HookFunc<BlobCommitBlockListBeforeHookContext> hook) => new(hook, _filter);

        public StorageHook<BlobCommitBlockListAfterHookContext> AfterCommitBlockList(HookFunc<BlobCommitBlockListAfterHookContext> hook) => new(hook, _filter);

    }


}


