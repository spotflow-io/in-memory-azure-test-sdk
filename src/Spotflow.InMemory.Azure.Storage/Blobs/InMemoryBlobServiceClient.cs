using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Spotflow.InMemory.Azure.Storage.Blobs.Internals;
using Spotflow.InMemory.Azure.Storage.Resources;

namespace Spotflow.InMemory.Azure.Storage.Blobs;

public class InMemoryBlobServiceClient : BlobServiceClient
{
    #region Constructors

    public InMemoryBlobServiceClient(string connectionString, InMemoryStorageProvider provider) : this(connectionString, null, provider) { }


    public InMemoryBlobServiceClient(Uri serviceUri, InMemoryStorageProvider provider) : this(null, serviceUri, provider) { }

    private InMemoryBlobServiceClient(string? connectionString, Uri? uri, InMemoryStorageProvider provider)
    {
        var builder = BlobUriUtils.BuilderForService(connectionString, uri, provider);

        Uri = builder.ToUri();
        Provider = provider;
    }

    public static InMemoryBlobServiceClient FromAccount(InMemoryStorageAccount account)
    {
        return new(account.BlobServiceUri, account.Provider);
    }

    #endregion

    public override Uri Uri { get; }
    public InMemoryStorageProvider Provider { get; }
    public override bool CanGenerateAccountSasUri => false;

    public override BlobContainerClient GetBlobContainerClient(string blobContainerName)
    {
        var blobContainerUri = BlobUriUtils.UriForContainer(Uri, blobContainerName);
        return new InMemoryBlobContainerClient(blobContainerUri, Provider);
    }

    #region Unsupported

    public override Pageable<BlobContainerItem> GetBlobContainers(BlobContainerTraits traits = BlobContainerTraits.None, BlobContainerStates states = BlobContainerStates.None, string? prefix = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Pageable<BlobContainerItem> GetBlobContainers(BlobContainerTraits traits, string prefix, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override AsyncPageable<BlobContainerItem> GetBlobContainersAsync(BlobContainerTraits traits = BlobContainerTraits.None, BlobContainerStates states = BlobContainerStates.None, string? prefix = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override AsyncPageable<BlobContainerItem> GetBlobContainersAsync(BlobContainerTraits traits, string prefix, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<AccountInfo> GetAccountInfo(CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<AccountInfo>> GetAccountInfoAsync(CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobServiceProperties> GetProperties(CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobServiceProperties>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response SetProperties(BlobServiceProperties properties, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> SetPropertiesAsync(BlobServiceProperties properties, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobServiceStatistics> GetStatistics(CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobServiceStatistics>> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<UserDelegationKey> GetUserDelegationKey(DateTimeOffset? startsOn, DateTimeOffset expiresOn, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<UserDelegationKey>> GetUserDelegationKeyAsync(DateTimeOffset? startsOn, DateTimeOffset expiresOn, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContainerClient> CreateBlobContainer(string blobContainerName, PublicAccessType publicAccessType = PublicAccessType.None, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContainerClient>> CreateBlobContainerAsync(string blobContainerName, PublicAccessType publicAccessType = PublicAccessType.None, IDictionary<string, string>? metadata = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response DeleteBlobContainer(string blobContainerName, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> DeleteBlobContainerAsync(string blobContainerName, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContainerClient> UndeleteBlobContainer(string deletedContainerName, string deletedContainerVersion, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContainerClient>> UndeleteBlobContainerAsync(string deletedContainerName, string deletedContainerVersion, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContainerClient> UndeleteBlobContainer(string deletedContainerName, string deletedContainerVersion, string destinationContainerName, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContainerClient>> UndeleteBlobContainerAsync(string deletedContainerName, string deletedContainerVersion, string destinationContainerName, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Pageable<TaggedBlobItem> FindBlobsByTags(string tagFilterSqlExpression, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override AsyncPageable<TaggedBlobItem> FindBlobsByTagsAsync(string tagFilterSqlExpression, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    #endregion
}
