using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.Storage.Blobs.Hooks;
using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;
using Spotflow.InMemory.Azure.Storage.Hooks.Internals;
using Spotflow.InMemory.Azure.Storage.Tables.Hooks;

namespace Spotflow.InMemory.Azure.Storage.Hooks;

public class StorageHookBuilder
{
    private readonly StorageHookFilter _filter;

    internal StorageHookBuilder(StorageHookFilter? filter = null)
    {
        _filter = filter ?? new();
    }

    public BlobServiceHookBuilder ForBlobService(string? storageAccountName = null) => new(_filter.With(storageAccountName));
    public TableServiceHookBuilder ForTableService(string? storageAccountName = null) => new(_filter.With(storageAccountName));

    public StorageHook<StorageBeforeHookContext> Before(HookFunc<StorageBeforeHookContext> hook, string? storageAccountName = null)
    {
        return new(hook, _filter.With(storageAccountName));
    }

    public StorageHook<StorageAfterHookContext> After(HookFunc<StorageAfterHookContext> hook, string? storageAccountName = null)
    {
        return new(hook, _filter.With(storageAccountName));
    }
}
