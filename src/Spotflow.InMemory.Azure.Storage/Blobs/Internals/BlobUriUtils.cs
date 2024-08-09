using Azure.Storage.Blobs;

using Spotflow.InMemory.Azure.Storage.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;
internal static class BlobUriUtils
{
    public static BlobUriBuilder BuilderForBlob(string? connectionString, Uri? uri, string? blobContainerName, string? blobName, InMemoryStorageProvider provider)
    {
        var builder = Builder(connectionString, uri, blobContainerName, blobName, provider);

        if (string.IsNullOrWhiteSpace(builder.BlobContainerName))
        {
            throw new InvalidOperationException("Blob container name must be specified when creating a blob client.");
        }

        if (string.IsNullOrWhiteSpace(builder.BlobName))
        {
            throw new InvalidOperationException("Blob name must be specified when creating a blob client.");
        }

        return builder;
    }

    public static BlobUriBuilder BuilderForContainer(string? connectionString, Uri? uri, string? blobContainerName, InMemoryStorageProvider provider)
    {
        var builder = Builder(connectionString, uri, blobContainerName, null, provider);

        if (string.IsNullOrWhiteSpace(builder.BlobContainerName))
        {
            throw new InvalidOperationException("Blob container name must be specified when creating a blob container client.");
        }

        return builder;
    }

    public static BlobUriBuilder BuilderForService(string? connectionString, Uri? uri, InMemoryStorageProvider provider)
    {
        var builder = Builder(connectionString, uri, null, null, provider);

        return builder;
    }

    public static Uri UriForBlob(Uri blobServiceUri, string blobContainerName, string blobName)
    {
        var builder = Builder(null, blobServiceUri, blobContainerName, blobName, null);

        return builder.ToUri();

    }

    public static Uri UriForContainer(Uri blobServiceUri, string blobContainerName)
    {
        var builder = Builder(null, blobServiceUri, blobContainerName, null, null);

        return builder.ToUri();
    }

    private static BlobUriBuilder Builder(string? connectionString, Uri? uri, string? blobContainerName, string? blobName, InMemoryStorageProvider? provider)
    {
        if (connectionString is not null && uri is not null)
        {
            throw new InvalidOperationException("Both a connection string and a URI cannot be provided.");
        }

        if (uri is null)
        {
            if (connectionString is null)
            {
                throw new InvalidOperationException("Either a connection string or a URI must be provided.");
            }

            if (provider is null)
            {
                throw new InvalidOperationException("A provider must be provided when using a connection string.");
            }

            var accountName = StorageConnectionStringUtils.GetAccountNameFromConnectionString(connectionString);
            uri = InMemoryBlobService.CreateServiceUriFromAccountName(accountName, provider);
        }

        var builder = new BlobUriBuilder(uri);

        if (blobContainerName is not null)
        {
            builder.BlobContainerName = blobContainerName;
        }

        if (blobName is not null)
        {
            builder.BlobName = blobName;
        }

        return builder;
    }
}
