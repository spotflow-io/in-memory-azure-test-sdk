
namespace Spotflow.InMemory.Azure.Storage.Hooks.Contexts;

public abstract class StorageBeforeHookContext(StorageAccountScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : StorageHookContext(scope.StorageAccountName, provider, cancellationToken);

