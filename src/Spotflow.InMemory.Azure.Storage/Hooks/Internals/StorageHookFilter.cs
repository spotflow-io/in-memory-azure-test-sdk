using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.Storage.Hooks.Internals;

internal record StorageHookFilter : BaseHookFilter<StorageHookContext>
{
    public string? StorageAccountName { get; private init; }

    public override bool Covers(StorageHookContext context)
    {
        return StorageAccountName is null || StorageAccountName == context.StorageAccountName;
    }

    public StorageHookFilter With(string? storageAccountName)
    {
        return this with { StorageAccountName = storageAccountName ?? StorageAccountName };
    }
}



