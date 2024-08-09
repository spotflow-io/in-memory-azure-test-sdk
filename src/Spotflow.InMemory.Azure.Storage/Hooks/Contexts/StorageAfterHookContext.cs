
namespace Spotflow.InMemory.Azure.Storage.Hooks.Contexts;

public abstract class StorageAfterHookContext(StorageBeforeHookContext before)
    : StorageHookContext(before.StorageAccountName, before.ResourceProvider, before.CancellationToken);
