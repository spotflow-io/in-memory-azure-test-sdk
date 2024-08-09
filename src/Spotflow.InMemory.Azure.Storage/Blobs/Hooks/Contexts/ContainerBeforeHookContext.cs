using Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public abstract class ContainerBeforeHookContext(BlobContainerScope scope, ContainerOperations operation, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : BlobServiceBeforeHookContext(scope, provider, cancellationToken), IContainerOperation
{
    public ContainerOperations Operation => operation;
    public string ContainerName => scope.ContainerName;
}
