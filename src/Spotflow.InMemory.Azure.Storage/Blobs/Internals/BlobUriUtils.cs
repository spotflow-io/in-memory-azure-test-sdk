using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

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

    public static Uri GenerateBlobSasUri(Uri blobUri, string containerName, string blobName, BlobSasBuilder sasBuilder, StorageSharedKeyCredential sharedKey)
    {
        var sasBuilderCopy = DeepCopy(sasBuilder);

        sasBuilderCopy.BlobContainerName = containerName;
        sasBuilderCopy.BlobName = blobName;

        var queryParameters = sasBuilderCopy.ToSasQueryParameters(sharedKey);

        var uriBuilder = new UriBuilder(blobUri)
        {
            Query = queryParameters.ToString()
        };

        return uriBuilder.Uri;
    }

    public static Uri GenerateContainerSasUri(Uri containerUri, string containerName, BlobSasBuilder sasBuilder, StorageSharedKeyCredential sharedKey)
    {
        var sasBuilderCopy = DeepCopy(sasBuilder);

        sasBuilderCopy.BlobContainerName = containerName;
        sasBuilderCopy.BlobName = null;

        var queryParameters = sasBuilderCopy.ToSasQueryParameters(sharedKey);

        var uriBuilder = new UriBuilder(containerUri)
        {
            Query = queryParameters.ToString()
        };

        return uriBuilder.Uri;
    }

    private static BlobSasBuilder DeepCopy(BlobSasBuilder builder)
    {
        var builderCopy = new BlobSasBuilder();

        builderCopy.SetPermissions(builder.Permissions);
        builderCopy.StartsOn = builder.StartsOn;
        builderCopy.ExpiresOn = builder.ExpiresOn;
        builderCopy.Protocol = builder.Protocol;
        builderCopy.IPRange = builder.IPRange;
        builderCopy.Identifier = builder.Identifier;
        builderCopy.PreauthorizedAgentObjectId = builder.PreauthorizedAgentObjectId;
        builderCopy.EncryptionScope = builder.EncryptionScope;
        builderCopy.Resource = builder.Resource;
        builderCopy.CacheControl = builder.CacheControl;
        builderCopy.ContentDisposition = builder.ContentDisposition;
        builderCopy.ContentEncoding = builder.ContentEncoding;
        builderCopy.ContentLanguage = builder.ContentLanguage;
        builderCopy.ContentType = builder.ContentType;
        builderCopy.Snapshot = builder.Snapshot;
        builderCopy.BlobVersionId = builder.BlobVersionId;
        builderCopy.CorrelationId = builder.CorrelationId;

        return builderCopy;
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
