namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Internals;

internal interface IBlobOperation
{
    public string ContainerName { get; }
    public string BlobName { get; }
    public BlobOperations Operation { get; }

}

