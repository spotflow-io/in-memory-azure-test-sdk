namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class ContainerCreateBeforeHookContext(BlobContainerScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : ContainerBeforeHookContext(scope, ContainerOperations.Create, provider, cancellationToken)
{
    public required bool CreateIfNotExists { get; init; }
}
