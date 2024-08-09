using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;
using Spotflow.InMemory.Azure.Storage.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Internals;

internal record BlobHookFilter : StorageHookFilter
{

    public BlobHookFilter(StorageHookFilter filter) : base(filter) { }

    public string? ContainerName { get; private init; }
    public string? BlobName { get; private init; }

    public ContainerOperations ContainerOperations { get; private init; } = ContainerOperations.All;
    public BlobOperations BlobOperations { get; private init; } = BlobOperations.All;

    public override bool Covers(StorageHookContext context)
    {
        var result = base.Covers(context);

        if (context is IContainerOperation container)
        {
            result &= ContainerName is null || container.ContainerName == ContainerName;
            result &= ContainerOperations.HasFlag(container.Operation);

            return result;
        }

        if (context is IBlobOperation blob)
        {
            result &= ContainerName is null || blob.ContainerName == ContainerName;
            result &= BlobName is null || blob.BlobName == BlobName;
            result &= BlobOperations.HasFlag(blob.Operation);

            return result;
        }

        throw new InvalidOperationException($"Unexpected context: {context}");
    }

    internal BlobHookFilter With(string? containerName = null, ContainerOperations? containerOperations = null, string? blobName = null, BlobOperations? blobOperations = null)
    {
        return this with
        {
            ContainerName = containerName ?? ContainerName,
            BlobName = blobName ?? BlobName,
            ContainerOperations = containerOperations ?? ContainerOperations,
            BlobOperations = blobOperations ?? BlobOperations
        };
    }

}



