using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class ContainerDeleteBeforeHookContext(BlobContainerScope scope, InMemoryStorageProvider provider, CancellationToken cancellationToken)
    : ContainerBeforeHookContext(scope, ContainerOperations.Delete, provider, cancellationToken)
{
    public required bool DeleteIfExists { get; init; }
    public BlobRequestConditions? Conditions { get; init; }
}
