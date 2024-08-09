using Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public abstract class ContainerAfterHookContext(ContainerBeforeHookContext before) : BlobServiceAfterHookContext(before), IContainerOperation
{
    public ContainerOperations Operation => before.Operation;
    public string ContainerName => before.ContainerName;
}
