namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Internals;

internal interface IContainerOperation
{
    public string ContainerName { get; }
    public ContainerOperations Operation { get; }
}
