using System.Web;

using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

using Spotflow.InMemory.Azure.Internals;
using Spotflow.InMemory.Azure.Storage.Blobs.Internals;
using Spotflow.InMemory.Azure.Storage.Resources;

namespace Spotflow.InMemory.Azure.Storage.Blobs;

public class InMemoryBlobClient : BlobClient
{
    private readonly BlobClientCore _core;

    #region Constructors

    public InMemoryBlobClient(string connectionString, string blobContainerName, string blobName, InMemoryStorageProvider provider)
        : this(connectionString, null, blobContainerName, blobName, provider) { }

    public InMemoryBlobClient(Uri blobUri, InMemoryStorageProvider provider)
        : this(null, blobUri, null, null, provider) { }

    private InMemoryBlobClient(string? connectionString, Uri? uri, string? blobContainerName, string? blobName, InMemoryStorageProvider provider)
    {
        var builder = BlobUriUtils.BuilderForBlob(connectionString, uri, blobContainerName, blobName, provider);
        _core = new(builder, provider);
    }

    public static InMemoryBlobClient FromAccount(InMemoryStorageAccount account, string blobContainerName, string blobName)
    {
        var blobUri = BlobUriUtils.UriForBlob(account.BlobServiceUri, blobContainerName, blobName);
        return new(blobUri, account.Provider);
    }

    #endregion

    public InMemoryStorageProvider Provider => _core.Provider;

    #region Properties

    public override Uri Uri => _core.Uri;
    public override string AccountName => _core.AccountName;
    public override string BlobContainerName => _core.BlobContainerName;
    public override string Name => _core.Name;
    public override bool CanGenerateSasUri => true;

    #endregion

    #region Clients

    protected override BlobContainerClient GetParentBlobContainerClientCore() => _core.GetParentContainerClient();

    #endregion

    #region Upload

    public override Response<BlobContentInfo> Upload(Stream content, BlobUploadOptions options, CancellationToken cancellationToken = default)
        => UploadCore(BinaryData.FromStream(content), options, null, cancellationToken);

    public override Response<BlobContentInfo> Upload(BinaryData content, BlobUploadOptions options, CancellationToken cancellationToken = default)
        => UploadCore(content, options, null, cancellationToken);

    public override Response<BlobContentInfo> Upload(Stream content)
        => UploadCore(BinaryData.FromStream(content), null, null, CancellationToken.None);

    public override Response<BlobContentInfo> Upload(BinaryData content)
        => UploadCore(content, null, null, CancellationToken.None);

    public override Task<Response<BlobContentInfo>> UploadAsync(Stream content)
        => UploadCoreAsync(BinaryData.FromStream(content), null, null, CancellationToken.None);

    public override Task<Response<BlobContentInfo>> UploadAsync(BinaryData content)
        => UploadCoreAsync(content, null, null, CancellationToken.None);

    public override Task<Response<BlobContentInfo>> UploadAsync(BinaryData content, BlobUploadOptions options, CancellationToken cancellationToken = default)
        => UploadCoreAsync(content, options, null, cancellationToken);

    public override Task<Response<BlobContentInfo>> UploadAsync(Stream content, BlobUploadOptions options, CancellationToken cancellationToken = default)
        => UploadCoreAsync(BinaryData.FromStream(content), options, null, cancellationToken);

    public override Response<BlobContentInfo> Upload(Stream content, CancellationToken cancellationToken)
        => UploadCore(BinaryData.FromStream(content), null, null, cancellationToken);

    public override Response<BlobContentInfo> Upload(BinaryData content, CancellationToken cancellationToken)
        => UploadCore(content, null, null, cancellationToken);

    public override Task<Response<BlobContentInfo>> UploadAsync(Stream content, CancellationToken cancellationToken)
        => UploadCoreAsync(BinaryData.FromStream(content), null, null, cancellationToken);

    public override Task<Response<BlobContentInfo>> UploadAsync(BinaryData content, CancellationToken cancellationToken)
        => UploadCoreAsync(content, null, null, cancellationToken);

    public override Response<BlobContentInfo> Upload(Stream content, bool overwrite = false, CancellationToken cancellationToken = default)
        => UploadCore(BinaryData.FromStream(content), null, overwrite, cancellationToken);

    public override Response<BlobContentInfo> Upload(BinaryData content, bool overwrite = false, CancellationToken cancellationToken = default)
        => UploadCore(content, null, overwrite, cancellationToken);

    public override Task<Response<BlobContentInfo>> UploadAsync(Stream content, bool overwrite = false, CancellationToken cancellationToken = default)
        => UploadCoreAsync(BinaryData.FromStream(content), null, overwrite, cancellationToken);

    public override Task<Response<BlobContentInfo>> UploadAsync(BinaryData content, bool overwrite = false, CancellationToken cancellationToken = default)
        => UploadCoreAsync(content, null, overwrite, cancellationToken);

    public override Response<BlobContentInfo> Upload(Stream content, BlobHttpHeaders? httpHeaders = null, IDictionary<string, string>? metadata = null, BlobRequestConditions? conditions = null, IProgress<long>? progressHandler = null, AccessTier? accessTier = null, StorageTransferOptions transferOptions = default, CancellationToken cancellationToken = default)
    {
        var options = new BlobUploadOptions
        {
            HttpHeaders = httpHeaders,
            Metadata = metadata,
            Conditions = conditions,
            ProgressHandler = progressHandler,
            AccessTier = accessTier,
            TransferOptions = transferOptions
        };

        return UploadCore(BinaryData.FromStream(content), options, null, cancellationToken);
    }

    public override Task<Response<BlobContentInfo>> UploadAsync(Stream content, BlobHttpHeaders? httpHeaders = null, IDictionary<string, string>? metadata = null, BlobRequestConditions? conditions = null, IProgress<long>? progressHandler = null, AccessTier? accessTier = null, StorageTransferOptions transferOptions = default, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Upload(content, httpHeaders, metadata, conditions, progressHandler, accessTier, transferOptions, cancellationToken));
    }

    private Response<BlobContentInfo> UploadCore(BinaryData content, BlobUploadOptions? options, bool? overwrite, CancellationToken cancellationToken)
    {
        var info = _core.UploadAsync(content, options, overwrite, cancellationToken).EnsureCompleted();
        return InMemoryResponse.FromValue(info, 201);
    }

    private async Task<Response<BlobContentInfo>> UploadCoreAsync(BinaryData content, BlobUploadOptions? options, bool? overwrite, CancellationToken cancellationToken)
    {
        var info = await _core.UploadAsync(content, options, overwrite, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        return InMemoryResponse.FromValue(info, 201);
    }

    #endregion

    #region Exists

    public override Response<bool> Exists(CancellationToken cancellationToken = default)
    {
        var exists = _core.Exists(cancellationToken);

        return exists switch
        {
            true => InMemoryResponse.FromValue(true, 200),
            false => InMemoryResponse.FromValue(false, 404)
        };
    }

    public override async Task<Response<bool>> ExistsAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return Exists(cancellationToken);
    }

    #endregion

    #region Get properties
    public override Response<BlobProperties> GetProperties(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        var properties = _core.GetProperties(conditions, cancellationToken);
        return InMemoryResponse.FromValue(properties, 200);
    }

    public override async Task<Response<BlobProperties>> GetPropertiesAsync(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return GetProperties(conditions, cancellationToken);
    }

    #endregion

    #region Download

    public override Response<BlobDownloadInfo> Download(CancellationToken cancellationToken = default)
    {
        var info = _core.DownloadAsync(null, cancellationToken).EnsureCompleted();
        return InMemoryResponse.FromValue(info, 200);
    }

    public override Response<BlobDownloadStreamingResult> DownloadStreaming(BlobDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = _core.DownloadStreamingAsync(options, cancellationToken).EnsureCompleted();
        return InMemoryResponse.FromValue(result, 200);
    }

    public override Response<BlobDownloadResult> DownloadContent(BlobDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = _core.DownloadContentAsync(options, cancellationToken).EnsureCompleted();
        return InMemoryResponse.FromValue(content, 200);
    }

    public override Response<BlobDownloadResult> DownloadContent(BlobRequestConditions conditions, CancellationToken cancellationToken)
    {
        var options = new BlobDownloadOptions { Conditions = conditions };
        return DownloadContent(options, cancellationToken);
    }

    public override Response<BlobDownloadInfo> Download() => Download(default);
    public override Task<Response<BlobDownloadInfo>> DownloadAsync() => DownloadAsync(default);

    public override async Task<Response<BlobDownloadInfo>> DownloadAsync(CancellationToken cancellationToken)
    {
        var info = await _core.DownloadAsync(null, cancellationToken: cancellationToken)
            .ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        return InMemoryResponse.FromValue(info, 200);
    }

    public override async Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(BlobDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await _core.DownloadStreamingAsync(options, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        return InMemoryResponse.FromValue(result, 200);
    }

    public override Response<BlobDownloadResult> DownloadContent() => DownloadContent((BlobDownloadOptions?) null, default);

    public override Response<BlobDownloadResult> DownloadContent(CancellationToken cancellationToken = default) => DownloadContent((BlobDownloadOptions?) null, cancellationToken);

    public override Task<Response<BlobDownloadResult>> DownloadContentAsync() => DownloadContentAsync(default);

    public override Task<Response<BlobDownloadResult>> DownloadContentAsync(CancellationToken cancellationToken)
    {
        return DownloadContentAsync((BlobDownloadOptions?) null, cancellationToken);
    }

    public override Task<Response<BlobDownloadResult>> DownloadContentAsync(BlobRequestConditions conditions, CancellationToken cancellationToken)
    {
        var options = new BlobDownloadOptions { Conditions = conditions };
        return DownloadContentAsync(options, cancellationToken);
    }

    public override async Task<Response<BlobDownloadResult>> DownloadContentAsync(BlobDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        var content = await _core.DownloadContentAsync(options, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        return InMemoryResponse.FromValue(content, 200);
    }

    #endregion

    #region Delete
    public override Response Delete(DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        return _core.Delete(snapshotsOption, conditions, cancellationToken);
    }

    public override async Task<Response> DeleteAsync(DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Delete(snapshotsOption, conditions, cancellationToken);
    }

    public override Response<bool> DeleteIfExists(DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        return _core.DeleteIfExists(snapshotsOption, conditions, cancellationToken);
    }

    public override async Task<Response<bool>> DeleteIfExistsAsync(DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return DeleteIfExists(snapshotsOption, conditions, cancellationToken);
    }

    #endregion

    #region OpenWrite

    public override Stream OpenWrite(bool overwrite, BlobOpenWriteOptions? options = null, CancellationToken cancellationToken = default)
    {
        return OpenWriteAsync(overwrite, options, cancellationToken).EnsureCompleted();
    }

    public override async Task<Stream> OpenWriteAsync(bool overwrite, BlobOpenWriteOptions? options = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return await _core.OpenWriteAsync(overwrite, options, cancellationToken);
    }

    #endregion

    #region OpenRead

    public override async Task<Stream> OpenReadAsync(BlobOpenReadOptions options, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return await _core.OpenReadAsync(options, cancellationToken);
    }

    public override Stream OpenRead(BlobOpenReadOptions options, CancellationToken cancellationToken = default)
    {
        return OpenReadAsync(options, cancellationToken).EnsureCompleted();
    }

    public override async Task<Stream> OpenReadAsync(long position = 0, int? bufferSize = null, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var options = new BlobOpenReadOptions(allowModifications: false)
        {
            Position = position,
            BufferSize = bufferSize,
            Conditions = conditions
        };

        return await _core.OpenReadAsync(options, cancellationToken);
    }

    public override Stream OpenRead(long position = 0, int? bufferSize = null, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        return OpenReadAsync(position, bufferSize, conditions, cancellationToken).EnsureCompleted();
    }


    public override async Task<Stream> OpenReadAsync(bool allowBlobModifications, long position = 0, int? bufferSize = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var options = new BlobOpenReadOptions(allowBlobModifications)
        {
            Position = position,
            BufferSize = bufferSize
        };

        return await _core.OpenReadAsync(options, cancellationToken);
    }

    public override Stream OpenRead(bool allowBlobModifications, long position = 0, int? bufferSize = null, CancellationToken cancellationToken = default)
    {
        return OpenReadAsync(allowBlobModifications, position, bufferSize, cancellationToken).EnsureCompleted();
    }

    #endregion


    #region Unsupported

    protected override BlobBaseClient WithSnapshotCore(string snapshot)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    protected override BlobLeaseClient GetBlobLeaseClientCore(string leaseId)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobDownloadInfo> Download(HttpRange range = default, BlobRequestConditions? conditions = null, bool rangeGetContentHash = false, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobDownloadInfo>> DownloadAsync(HttpRange range = default, BlobRequestConditions? conditions = null, bool rangeGetContentHash = false, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobDownloadStreamingResult> DownloadStreaming(HttpRange range, BlobRequestConditions conditions, bool rangeGetContentHash, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(HttpRange range, BlobRequestConditions conditions, bool rangeGetContentHash, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobDownloadStreamingResult> DownloadStreaming(HttpRange range, BlobRequestConditions conditions, bool rangeGetContentHash, IProgress<long> progressHandler, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(HttpRange range, BlobRequestConditions conditions, bool rangeGetContentHash, IProgress<long> progressHandler, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobDownloadResult> DownloadContent(BlobRequestConditions conditions, IProgress<long> progressHandler, HttpRange range, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobDownloadResult>> DownloadContentAsync(BlobRequestConditions conditions, IProgress<long> progressHandler, HttpRange range, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response DownloadTo(Stream destination)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response DownloadTo(string path)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> DownloadToAsync(Stream destination)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> DownloadToAsync(string path)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response DownloadTo(Stream destination, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response DownloadTo(string path, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> DownloadToAsync(Stream destination, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> DownloadToAsync(string path, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response DownloadTo(Stream destination, BlobDownloadToOptions options, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response DownloadTo(string path, BlobDownloadToOptions options, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> DownloadToAsync(Stream destination, BlobDownloadToOptions options, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> DownloadToAsync(string path, BlobDownloadToOptions options, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response DownloadTo(Stream destination, BlobRequestConditions? conditions = null, StorageTransferOptions transferOptions = default, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response DownloadTo(string path, BlobRequestConditions? conditions = null, StorageTransferOptions transferOptions = default, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> DownloadToAsync(Stream destination, BlobRequestConditions? conditions = null, StorageTransferOptions transferOptions = default, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> DownloadToAsync(string path, BlobRequestConditions? conditions = null, StorageTransferOptions transferOptions = default, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override CopyFromUriOperation StartCopyFromUri(Uri source, BlobCopyFromUriOptions options, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override CopyFromUriOperation StartCopyFromUri(Uri source, IDictionary<string, string>? metadata = null, AccessTier? accessTier = null, BlobRequestConditions? sourceConditions = null, BlobRequestConditions? destinationConditions = null, RehydratePriority? rehydratePriority = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<CopyFromUriOperation> StartCopyFromUriAsync(Uri source, BlobCopyFromUriOptions options, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<CopyFromUriOperation> StartCopyFromUriAsync(Uri source, IDictionary<string, string>? metadata = null, AccessTier? accessTier = null, BlobRequestConditions? sourceConditions = null, BlobRequestConditions? destinationConditions = null, RehydratePriority? rehydratePriority = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response AbortCopyFromUri(string copyId, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> AbortCopyFromUriAsync(string copyId, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobCopyInfo> SyncCopyFromUri(Uri source, BlobCopyFromUriOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobCopyInfo>> SyncCopyFromUriAsync(Uri source, BlobCopyFromUriOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response Undelete(CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> UndeleteAsync(CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobInfo> SetHttpHeaders(BlobHttpHeaders? httpHeaders = null, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobInfo>> SetHttpHeadersAsync(BlobHttpHeaders? httpHeaders = null, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobInfo> SetMetadata(IDictionary<string, string> metadata, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobInfo>> SetMetadataAsync(IDictionary<string, string> metadata, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobSnapshotInfo> CreateSnapshot(IDictionary<string, string>? metadata = null, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobSnapshotInfo>> CreateSnapshotAsync(IDictionary<string, string>? metadata = null, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response SetAccessTier(AccessTier accessTier, BlobRequestConditions? conditions = null, RehydratePriority? rehydratePriority = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> SetAccessTierAsync(AccessTier accessTier, BlobRequestConditions? conditions = null, RehydratePriority? rehydratePriority = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<GetBlobTagResult> GetTags(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<GetBlobTagResult>> GetTagsAsync(BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response SetTags(IDictionary<string, string> tags, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> SetTagsAsync(IDictionary<string, string> tags, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobImmutabilityPolicy> SetImmutabilityPolicy(BlobImmutabilityPolicy immutabilityPolicy, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobImmutabilityPolicy>> SetImmutabilityPolicyAsync(BlobImmutabilityPolicy immutabilityPolicy, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response DeleteImmutabilityPolicy(CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> DeleteImmutabilityPolicyAsync(CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobLegalHoldResult> SetLegalHold(bool hasLegalHold, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobLegalHoldResult>> SetLegalHoldAsync(bool hasLegalHold, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Uri GenerateSasUri(BlobSasPermissions permissions, DateTimeOffset expiresOn)
    {
        var uriBuilder = new UriBuilder(Uri)
        {
            Query = $"sv=2024-05-04&se={HttpUtility.UrlEncode(expiresOn.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}&sr=b&sp=r&sig=xxx"
        };

        return uriBuilder.Uri;
    }

    public override Uri GenerateSasUri(BlobSasBuilder builder)
    {
        var uriBuilder = new UriBuilder(Uri)
        {
            Query = $"sv=2024-05-04&se={HttpUtility.UrlEncode(builder.ExpiresOn.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}&sr=b&sp=r&sig=xxx"
        };

        return uriBuilder.Uri;
    }

    protected override BlobClient WithClientSideEncryptionOptionsCore(ClientSideEncryptionOptions clientSideEncryptionOptions)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContentInfo> Upload(string path)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContentInfo>> UploadAsync(string path)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContentInfo> Upload(string path, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContentInfo>> UploadAsync(string path, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContentInfo> Upload(string path, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContentInfo>> UploadAsync(string path, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContentInfo> Upload(string path, BlobUploadOptions options, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContentInfo> Upload(string path, BlobHttpHeaders? httpHeaders = null, IDictionary<string, string>? metadata = null, BlobRequestConditions? conditions = null, IProgress<long>? progressHandler = null, AccessTier? accessTier = null, StorageTransferOptions transferOptions = default, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContentInfo>> UploadAsync(string path, BlobUploadOptions options, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContentInfo>> UploadAsync(string path, BlobHttpHeaders? httpHeaders = null, IDictionary<string, string>? metadata = null, BlobRequestConditions? conditions = null, IProgress<long>? progressHandler = null, AccessTier? accessTier = null, StorageTransferOptions transferOptions = default, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    #endregion
}

