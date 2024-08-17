using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

using static Tests.Storage.Blobs.BlobClientTests;

namespace Tests.Utils;

internal static class BlobContainerClientExtensions
{
    public static BlobBaseClient GetBlobBaseClient(this BlobContainerClient containerClient, string blobName, BlobClientType type)
    {
        return type switch
        {
            BlobClientType.Generic => containerClient.GetBlobClient(blobName),
            BlobClientType.Block => containerClient.GetBlockBlobClient(blobName),
        };
    }
}

