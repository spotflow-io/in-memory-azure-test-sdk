using System.Diagnostics.CodeAnalysis;

using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

using Spotflow.InMemory.Azure.Internals;
using Spotflow.InMemory.Azure.Storage.Blobs.Hooks;
using Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;
using Spotflow.InMemory.Azure.Storage.Blobs.Internals;
using Spotflow.InMemory.Azure.Storage.Internals;
using Spotflow.InMemory.Azure.Storage.Resources;

namespace Spotflow.InMemory.Azure.Storage.Blobs;

public class InMemoryBlobContainerClient : BlobContainerClient
{
    private const int _defaultMaxPageSize = 5000;

    private readonly BlobContainerScope _scope;

    private readonly StorageSharedKeyCredential? _sharedKey;
    #region Constructors

    public InMemoryBlobContainerClient(string connectionString, string blobContainerName, InMemoryStorageProvider provider)
        : this(connectionString, null, blobContainerName, provider) { }

    public InMemoryBlobContainerClient(Uri blobContainerUri, InMemoryStorageProvider provider)
        : this(null, blobContainerUri, null, provider) { }

    public static InMemoryBlobContainerClient FromAccount(InMemoryStorageAccount account, string blobContainerName, bool useConnectionString = false)
    {
        if (useConnectionString)
        {
            return new(connectionString: account.GetConnectionString(), blobContainerName, account.Provider);
        }
        else
        {
            var blobContainerUri = BlobUriUtils.UriForContainer(account.BlobServiceUri, blobContainerName);
            return new(blobContainerUri, account.Provider);
        }
    }

    private InMemoryBlobContainerClient(string? connectionString, Uri? uri, string? blobContainerName, InMemoryStorageProvider provider)
    {
        var builder = BlobUriUtils.BuilderForContainer(connectionString, uri, blobContainerName, provider);

        Uri = builder.ToUri();
        AccountName = builder.AccountName;
        Name = builder.BlobContainerName;
        Provider = provider;
        _scope = new(builder.AccountName, builder.BlobContainerName);

        if (connectionString is not null && StorageConnectionStringUtils.TryGetSharedKey(connectionString, out var sharedKey))
        {
            _sharedKey = sharedKey;
        }

    }

    #endregion


    #region Properties

    public override Uri Uri { get; }
    public override string AccountName { get; }
    public override string Name { get; }

    [MemberNotNullWhen(true, nameof(_sharedKey))]
    public override bool CanGenerateSasUri => _sharedKey is not null;

    #endregion

    public InMemoryStorageProvider Provider { get; }

    #region Get Client

    protected override BlobServiceClient GetParentBlobServiceClientCore()
    {
        var serviceUri = Provider.GetAccount(AccountName).BlobServiceUri;
        return new InMemoryBlobServiceClient(serviceUri, Provider);
    }

    public override BlobClient GetBlobClient(string blobName)
    {
        var blobUri = BlobUriUtils.UriForBlob(Uri, Name, blobName);
        return new InMemoryBlobClient(blobUri, Provider);
    }

    protected override BlobBaseClient GetBlobBaseClientCore(string blobName) => GetBlobClient(blobName);

    protected override BlockBlobClient GetBlockBlobClientCore(string blobName)
    {
        var blobUri = BlobUriUtils.UriForBlob(Uri, Name, blobName);
        return new InMemoryBlockBlobClient(blobUri, Provider);
    }

    #endregion

    #region Create If Not Exists

    public override Response<BlobContainerInfo> CreateIfNotExists(PublicAccessType publicAccessType = PublicAccessType.None, IDictionary<string, string>? metadata = null, BlobContainerEncryptionScopeOptions? encryptionScopeOptions = null, CancellationToken cancellationToken = default)
    {
        return CreateIfNotExistsAsync(publicAccessType, metadata, encryptionScopeOptions, cancellationToken).EnsureCompleted();
    }

    public override async Task<Response<BlobContainerInfo>> CreateIfNotExistsAsync(PublicAccessType publicAccessType = PublicAccessType.None, IDictionary<string, string>? metadata = null, BlobContainerEncryptionScopeOptions? encryptionScopeOptions = null, CancellationToken cancellationToken = default)
    {
        (var info, var added) = await CreateIfNotExistsCoreAsync(metadata, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);

        return added switch
        {
            true => InMemoryResponse.FromValue(info, 201),
            false => InMemoryResponse.FromValue(info, 409)
        };
    }

    public override Task<Response<BlobContainerInfo>> CreateIfNotExistsAsync(PublicAccessType publicAccessType, IDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        return CreateIfNotExistsAsync(publicAccessType, metadata, null, cancellationToken);
    }

    public override Response<BlobContainerInfo> CreateIfNotExists(PublicAccessType publicAccessType, IDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        return CreateIfNotExists(publicAccessType, metadata, null, cancellationToken);
    }

    private async Task<(BlobContainerInfo, bool)> CreateIfNotExistsCoreAsync(IDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var beforeContext = new ContainerCreateBeforeHookContext(_scope, Provider, cancellationToken)
        {
            CreateIfNotExists = true
        };

        await ExecuteBeforeHooksAsync(beforeContext).ConfigureAwait(ConfigureAwaitOptions.None);

        var blobService = GetBlobService();

        if (!blobService.TryAddBlobContainer(Name, metadata, out var container, out var error))
        {
            if (error is InMemoryBlobService.CreateContainerError.ContainerAlreadyExists containerAlreadyExists)
            {
                return (GetInfo(containerAlreadyExists.ExistingContainer), false);
            }

            throw error.GetClientException();
        }

        var containerInfo = GetInfo(container);

        var afterContext = new ContainerCreateAfterHookContext(beforeContext)
        {
            ContainerInfo = containerInfo
        };

        await ExecuteAfterHooksAsync(afterContext).ConfigureAwait(ConfigureAwaitOptions.None);

        return (containerInfo, true);

    }

    #endregion

    #region Create

    public override Response<BlobContainerInfo> Create(PublicAccessType publicAccessType = PublicAccessType.None, IDictionary<string, string>? metadata = null, BlobContainerEncryptionScopeOptions? encryptionScopeOptions = null, CancellationToken cancellationToken = default)
    {
        var info = CreateCoreAsync(metadata, cancellationToken).EnsureCompleted();
        return InMemoryResponse.FromValue(info, 201);
    }

    public override Response<BlobContainerInfo> Create(PublicAccessType publicAccessType, IDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        return Create(publicAccessType, metadata, null, cancellationToken);
    }

    public override async Task<Response<BlobContainerInfo>> CreateAsync(PublicAccessType publicAccessType = PublicAccessType.None, IDictionary<string, string>? metadata = null, BlobContainerEncryptionScopeOptions? encryptionScopeOptions = null, CancellationToken cancellationToken = default)
    {
        var info = await CreateCoreAsync(metadata, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        return InMemoryResponse.FromValue(info, 201);
    }

    public override Task<Response<BlobContainerInfo>> CreateAsync(PublicAccessType publicAccessType, IDictionary<string, string> metadata, CancellationToken cancellationToken)
    {
        return CreateAsync(publicAccessType, metadata, null, cancellationToken);
    }

    private async Task<BlobContainerInfo> CreateCoreAsync(IDictionary<string, string>? metadata, CancellationToken cancellationToken)
    {
        var beforeContext = new ContainerCreateBeforeHookContext(_scope, Provider, cancellationToken)
        {
            CreateIfNotExists = false
        };

        await ExecuteBeforeHooksAsync(beforeContext).ConfigureAwait(ConfigureAwaitOptions.None);

        var blobService = GetBlobService();

        if (!blobService.TryAddBlobContainer(Name, metadata, out var container, out var error))
        {
            throw error.GetClientException();
        }

        var result = GetInfo(container);

        var afterContext = new ContainerCreateAfterHookContext(beforeContext)
        {
            ContainerInfo = result
        };

        await ExecuteAfterHooksAsync(afterContext).ConfigureAwait(ConfigureAwaitOptions.None);

        return result;
    }

    #endregion

    #region Get Blobs

    public override AsyncPageable<BlobItem> GetBlobsAsync(
        BlobTraits traits = BlobTraits.None,
        BlobStates states = BlobStates.None,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var blobs = GetBlobsCore(prefix, traits, states);
        return new InMemoryPageable.YieldingAsync<BlobItem>(blobs, _defaultMaxPageSize);
    }

    public override Pageable<BlobItem> GetBlobs(
        BlobTraits traits = BlobTraits.None,
        BlobStates states = BlobStates.None,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var blobs = GetBlobsCore(prefix, traits, states);
        return new InMemoryPageable.Sync<BlobItem>(blobs, _defaultMaxPageSize);
    }


    private IReadOnlyList<BlobItem> GetBlobsCore(string? prefix, BlobTraits traits, BlobStates states)
    {
        var container = GetContainer();

        return container.GetBlobs(prefix, traits, states);
    }

    #endregion

    #region Get Blobs By Hierarchy
    public override Pageable<BlobHierarchyItem> GetBlobsByHierarchy(
        BlobTraits traits = BlobTraits.None,
        BlobStates states = BlobStates.None,
        string? delimiter = null,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var items = GetBlobsByHierarchyCoreAsync(traits,states, delimiter, prefix, cancellationToken).EnsureCompleted();
        return new InMemoryPageable.Sync<BlobHierarchyItem>(items, _defaultMaxPageSize);
    }

    public override AsyncPageable<BlobHierarchyItem> GetBlobsByHierarchyAsync(
        BlobTraits traits = BlobTraits.None,
        BlobStates states = BlobStates.None,
        string? delimiter = null,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var items = GetBlobsByHierarchyCoreAsync(traits,states, delimiter, prefix, cancellationToken).EnsureCompleted();
        return new InMemoryPageable.YieldingAsync<BlobHierarchyItem>(items, _defaultMaxPageSize);
    }

    private async Task<IReadOnlyList<BlobHierarchyItem>> GetBlobsByHierarchyCoreAsync(
        BlobTraits traits,
        BlobStates states,
        string? delimiter,
        string? prefix,
        CancellationToken cancellationToken)
    {
        await Task.Yield();

        var blobs = GetBlobsCore(prefix, traits,states);

        if (delimiter is null)
        {
            return blobs.Select(i => BlobsModelFactory.BlobHierarchyItem(null, i)).ToArray();
        }

        var rootDirectories = new List<BlobHierarchyItem>();
        var rootBlobs = new List<BlobHierarchyItem>();

        string? previousDirectory = null;

        foreach (var blob in blobs)
        {
            var name = blob.Name[(prefix?.Length ?? 0)..];

            var delimiterIndex = name.IndexOf(delimiter, StringComparison.Ordinal);

            if (delimiterIndex is -1)
            {
                rootBlobs.Add(BlobsModelFactory.BlobHierarchyItem(null, blob));
            }
            else
            {
                var currentDirectory = name[..delimiterIndex];

                if (previousDirectory != currentDirectory)
                {
                    rootDirectories.Add(BlobsModelFactory.BlobHierarchyItem($"{prefix}{currentDirectory}{delimiter}", null));
                    previousDirectory = currentDirectory;
                }

            }
        }

        return [.. rootDirectories, .. rootBlobs];
    }

    #endregion

    #region Exists

    public override Response<bool> Exists(CancellationToken cancellationToken = default)
    {
        var service = GetBlobService();

        return service.ContainerExists(Name) switch
        {
            true => InMemoryResponse.FromValue(true, 200),
            false => InMemoryResponse.FromValue(false, 404)
        };
    }

    public override Task<Response<bool>> ExistsAsync(CancellationToken cancellationToken = default)
    {
        var result = Exists(cancellationToken);
        return Task.FromResult(result);
    }

    #endregion

    #region Get Properties

    public override Response<BlobContainerProperties> GetProperties(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        var container = GetContainer();

        CheckConditions(container.GetProperties().ETag, conditions);

        return InMemoryResponse.FromValue(container.GetProperties(), 200);
    }

    public override async Task<Response<BlobContainerProperties>> GetPropertiesAsync(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return GetProperties(conditions, cancellationToken);
    }

    #endregion

    #region Upload Blob
    public override Response<BlobContentInfo> UploadBlob(string blobName, Stream content, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(blobName);
        return blobClient.Upload(content, cancellationToken);
    }

    public override async Task<Response<BlobContentInfo>> UploadBlobAsync(string blobName, Stream content, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(blobName);
        return await blobClient.UploadAsync(content, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    public override Response<BlobContentInfo> UploadBlob(string blobName, BinaryData content, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(blobName);
        return blobClient.Upload(content, cancellationToken);
    }

    public override async Task<Response<BlobContentInfo>> UploadBlobAsync(string blobName, BinaryData content, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(blobName);
        return await blobClient.UploadAsync(content, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    #endregion

    #region Delete Blob

    public override Response DeleteBlob(string blobName, DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobBaseClientCore(blobName);
        return blobClient.Delete(snapshotsOption, conditions, cancellationToken);
    }

    public override async Task<Response> DeleteBlobAsync(string blobName, DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return DeleteBlob(blobName, snapshotsOption, conditions, cancellationToken);
    }

    public override Response<bool> DeleteBlobIfExists(string blobName, DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobBaseClientCore(blobName);
        return blobClient.DeleteIfExists(snapshotsOption, conditions, cancellationToken);
    }

    public override async Task<Response<bool>> DeleteBlobIfExistsAsync(string blobName, DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return DeleteBlobIfExists(blobName, snapshotsOption, conditions, cancellationToken);
    }

    #endregion

    #region SAS

    public override Uri GenerateSasUri(BlobContainerSasPermissions permissions, DateTimeOffset expiresOn)
    {
        var blobSasBuilder = new BlobSasBuilder(permissions, expiresOn);
        return GenerateSasUri(blobSasBuilder);
    }

    public override Uri GenerateSasUri(BlobSasBuilder builder)
    {
        if (!CanGenerateSasUri)
        {
            throw BlobExceptionFactory.SharedKeyCredentialNotSet();
        }

        return BlobUriUtils.GenerateContainerSasUri(Uri, Name, builder, _sharedKey);
    }

    #endregion

    private InMemoryBlobService GetBlobService()
    {
        if (!Provider.TryGetAccount(AccountName, out var account))
        {
            throw BlobExceptionFactory.BlobServiceNotFound(AccountName, Provider);
        }

        return account.BlobService;
    }

    private void CheckConditions(ETag? currentETag, BlobRequestConditions? conditions)
    {
        if (!ConditionChecker.CheckConditions(currentETag, conditions?.IfMatch, conditions?.IfNoneMatch, out var error))
        {
            throw BlobExceptionFactory.ConditionNotMet(error.ConditionType, AccountName, Name, error.Message);
        }
    }

    private InMemoryBlobContainer GetContainer()
    {
        var blobService = GetBlobService();

        if (!blobService.TryGetBlobContainer(Name, out var container))
        {
            throw BlobExceptionFactory.ContainerNotFound(Name, blobService);
        }

        return container;
    }

    private static BlobContainerInfo GetInfo(InMemoryBlobContainer container)
    {
        var properties = container.GetProperties();
        return BlobsModelFactory.BlobContainerInfo(properties.ETag, properties.LastModified);
    }

    #region Unsupported

    protected override AppendBlobClient GetAppendBlobClientCore(string blobName)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    protected override PageBlobClient GetPageBlobClientCore(string blobName)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    protected override BlobLeaseClient GetBlobLeaseClientCore(string leaseId)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response Delete(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> DeleteAsync(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<bool> DeleteIfExists(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<bool>> DeleteIfExistsAsync(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContainerInfo> SetMetadata(IDictionary<string, string> metadata, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContainerInfo>> SetMetadataAsync(IDictionary<string, string> metadata, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContainerAccessPolicy> GetAccessPolicy(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContainerAccessPolicy>> GetAccessPolicyAsync(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContainerInfo> SetAccessPolicy(PublicAccessType accessType = PublicAccessType.None, IEnumerable<BlobSignedIdentifier>? permissions = null, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContainerInfo>> SetAccessPolicyAsync(PublicAccessType accessType = PublicAccessType.None, IEnumerable<BlobSignedIdentifier>? permissions = null, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
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

    private Task ExecuteBeforeHooksAsync<TContext>(TContext context) where TContext : ContainerBeforeHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

    private Task ExecuteAfterHooksAsync<TContext>(TContext context) where TContext : ContainerAfterHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }


}
