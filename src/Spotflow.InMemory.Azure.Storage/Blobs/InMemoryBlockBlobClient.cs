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

public class InMemoryBlockBlobClient : BlockBlobClient
{
    #region Constructors

    private readonly BlobClientCore _core;

    public InMemoryBlockBlobClient(string connectionString, string blobContainerName, string blobName, InMemoryStorageProvider provider)
        : this(connectionString, null, blobContainerName, blobName, provider) { }

    public InMemoryBlockBlobClient(Uri blobUri, InMemoryStorageProvider provider)
        : this(null, blobUri, null, null, provider) { }

    private InMemoryBlockBlobClient(string? connectionString, Uri? uri, string? blobContainerName, string? blobName, InMemoryStorageProvider provider)
    {
        var builder = BlobUriUtils.BuilderForBlob(connectionString, uri, blobContainerName, blobName, provider);
        _core = new(builder, provider);
    }

    public static InMemoryBlockBlobClient FromAccount(InMemoryStorageAccount account, string blobContainerName, string blobName)
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
    public override bool CanGenerateSasUri => false;

    public override int BlockBlobMaxUploadBlobBytes => InMemoryBlobService.MaxBlockSize;

    public override long BlockBlobMaxUploadBlobLongBytes => InMemoryBlobService.MaxBlockSize;

    public override int BlockBlobMaxStageBlockBytes => InMemoryBlobService.MaxBlockSize;

    public override long BlockBlobMaxStageBlockLongBytes => InMemoryBlobService.MaxBlockSize;

    public override int BlockBlobMaxBlocks => InMemoryBlobService.MaxBlockCount;

    #endregion

    #region Clients

    protected override BlobContainerClient GetParentBlobContainerClientCore() => _core.GetParentContainerClient();

    #endregion

    #region Get Block List

    public override Response<BlockList> GetBlockList(BlockListTypes blockListTypes = BlockListTypes.All, string? snapshot = null, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        var blockList = _core.GetBlockList(blockListTypes, conditions, cancellationToken);
        return InMemoryResponse.FromValue(blockList, 200);
    }

    public override async Task<Response<BlockList>> GetBlockListAsync(BlockListTypes blockListTypes = BlockListTypes.All, string? snapshot = null, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return GetBlockList(blockListTypes, snapshot, conditions, cancellationToken);
    }

    #endregion

    #region Stage Block

    public override Response<BlockInfo> StageBlock(string base64BlockId, Stream content, BlockBlobStageBlockOptions? options = null, CancellationToken cancellationToken = default)
    {
        var blockInfo = _core.StageBlock(base64BlockId, BinaryData.FromStream(content), options, cancellationToken);
        return InMemoryResponse.FromValue(blockInfo, 201);
    }

    public override Response<BlockInfo> StageBlock(string base64BlockId, Stream content, byte[] transactionalContentHash, BlobRequestConditions conditions, IProgress<long> progressHandler, CancellationToken cancellationToken)
    {
        var options = new BlockBlobStageBlockOptions
        {
            Conditions = conditions
        };

        return StageBlock(base64BlockId, content, options, cancellationToken);
    }

    public override async Task<Response<BlockInfo>> StageBlockAsync(string base64BlockId, Stream content, byte[] transactionalContentHash, BlobRequestConditions conditions, IProgress<long> progressHandler, CancellationToken cancellationToken)
    {
        await Task.Yield();
        return StageBlock(base64BlockId, content, transactionalContentHash, conditions, progressHandler, cancellationToken);
    }

    public override async Task<Response<BlockInfo>> StageBlockAsync(string base64BlockId, Stream content, BlockBlobStageBlockOptions? options = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return StageBlock(base64BlockId, content, options, cancellationToken);
    }

    #endregion

    #region Commit Block List

    public override Response<BlobContentInfo> CommitBlockList(
        IEnumerable<string> base64BlockIds,
        CommitBlockListOptions options,
        CancellationToken cancellationToken = default)
    {
        var contentInfo = _core.CommitBlockList(base64BlockIds, options, cancellationToken);
        return InMemoryResponse.FromValue(contentInfo, 201);
    }

    public override Response<BlobContentInfo> CommitBlockList(
      IEnumerable<string> base64BlockIds,
      BlobHttpHeaders? httpHeaders = null,
      IDictionary<string, string>? metadata = null,
      BlobRequestConditions? conditions = null,
      AccessTier? accessTier = null,
      CancellationToken cancellationToken = default)
    {
        var options = new CommitBlockListOptions { HttpHeaders = httpHeaders, Metadata = metadata, Conditions = conditions, AccessTier = accessTier };

        return CommitBlockList(base64BlockIds, options, cancellationToken);
    }

    public override async Task<Response<BlobContentInfo>> CommitBlockListAsync(IEnumerable<string> base64BlockIds, CommitBlockListOptions options, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return CommitBlockList(base64BlockIds, options, cancellationToken);

    }

    public override async Task<Response<BlobContentInfo>> CommitBlockListAsync(IEnumerable<string> base64BlockIds, BlobHttpHeaders? httpHeaders = null, IDictionary<string, string>? metadata = null, BlobRequestConditions? conditions = null, AccessTier? accessTier = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return CommitBlockList(base64BlockIds, httpHeaders, metadata, conditions, accessTier, cancellationToken);
    }

    #endregion

    #region Upload

    public override Response<BlobContentInfo> Upload(Stream content, BlobUploadOptions options, CancellationToken cancellationToken = default)
    {
        var info = _core.UploadAsync(BinaryData.FromStream(content), options, null, cancellationToken).EnsureCompleted();
        return InMemoryResponse.FromValue(info, 201);
    }

    public override Response<BlobContentInfo> Upload(Stream content, BlobHttpHeaders? httpHeaders = null, IDictionary<string, string>? metadata = null, BlobRequestConditions? conditions = null, AccessTier? accessTier = null, IProgress<long>? progressHandler = null, CancellationToken cancellationToken = default)
    {
        var options = new BlobUploadOptions
        {
            HttpHeaders = httpHeaders,
            Metadata = metadata,
            Conditions = conditions,
            AccessTier = accessTier,
            ProgressHandler = progressHandler
        };
        return Upload(content, options, cancellationToken);
    }

    public override async Task<Response<BlobContentInfo>> UploadAsync(Stream content, BlobUploadOptions options, CancellationToken cancellationToken = default)
    {
        var info = await _core.UploadAsync(BinaryData.FromStream(content), options, null, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        return InMemoryResponse.FromValue(info, 201);
    }

    public override Task<Response<BlobContentInfo>> UploadAsync(Stream content, BlobHttpHeaders? httpHeaders = null, IDictionary<string, string>? metadata = null, BlobRequestConditions? conditions = null, AccessTier? accessTier = null, IProgress<long>? progressHandler = null, CancellationToken cancellationToken = default)
    {
        var options = new BlobUploadOptions
        {
            HttpHeaders = httpHeaders,
            Metadata = metadata,
            Conditions = conditions,
            AccessTier = accessTier,
            ProgressHandler = progressHandler
        };
        return UploadAsync(content, options, cancellationToken);
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
        var info = await _core.DownloadAsync(null, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        return InMemoryResponse.FromValue(info, 200);
    }

    public override async Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(BlobDownloadOptions? options = null, CancellationToken cancellationToken = default)
    {
        var result = await _core.DownloadStreamingAsync(options, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        return InMemoryResponse.FromValue(result, 200);
    }

    public override Response<BlobDownloadResult> DownloadContent() => DownloadContent((BlobDownloadOptions?) null, default);

    public override Task<Response<BlobDownloadResult>> DownloadContentAsync() => DownloadContentAsync(default);

    public override Response<BlobDownloadResult> DownloadContent(CancellationToken cancellationToken = default) => DownloadContent((BlobDownloadOptions?) null, cancellationToken);

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

    #region OpenWrite

    public override Stream OpenWrite(bool overwrite, BlockBlobOpenWriteOptions? options = null, CancellationToken cancellationToken = default)
    {
        return OpenWriteAsync(overwrite, options, cancellationToken).EnsureCompleted();
    }

    public override async Task<Stream> OpenWriteAsync(bool overwrite, BlockBlobOpenWriteOptions? options = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var unifiedOptions = new BlobOpenWriteOptions
        {
            BufferSize = options?.BufferSize,
            OpenConditions = options?.OpenConditions,
            HttpHeaders = options?.HttpHeaders,
            Metadata = options?.Metadata,
            ProgressHandler = options?.ProgressHandler,
            TransferValidation = options?.TransferValidation,
            Tags = options?.Tags
        };

        return await _core.OpenWriteAsync(overwrite, unifiedOptions, cancellationToken);
    }

    #endregion

    #region Delete

    public override Response Delete(DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        return _core.Delete(snapshotsOption, conditions, cancellationToken);
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

    public override async Task<Response> DeleteAsync(DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None, BlobRequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Delete(snapshotsOption, conditions, cancellationToken);
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
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Uri GenerateSasUri(BlobSasBuilder builder)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlockInfo> StageBlockFromUri(Uri sourceUri, string base64BlockId, StageBlockFromUriOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlockInfo>> StageBlockFromUriAsync(Uri sourceUri, string base64BlockId, StageBlockFromUriOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlockInfo> StageBlockFromUri(Uri sourceUri, string base64BlockId, HttpRange sourceRange, byte[] sourceContentHash, RequestConditions sourceConditions, BlobRequestConditions conditions, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlockInfo>> StageBlockFromUriAsync(Uri sourceUri, string base64BlockId, HttpRange sourceRange, byte[] sourceContentHash, RequestConditions sourceConditions, BlobRequestConditions conditions, CancellationToken cancellationToken)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobDownloadInfo> Query(string querySqlExpression, BlobQueryOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobDownloadInfo>> QueryAsync(string querySqlExpression, BlobQueryOptions? options = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContentInfo> SyncUploadFromUri(Uri copySource, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContentInfo>> SyncUploadFromUriAsync(Uri copySource, bool overwrite = false, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response<BlobContentInfo> SyncUploadFromUri(Uri copySource, BlobSyncUploadFromUriOptions options, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<BlobContentInfo>> SyncUploadFromUriAsync(Uri copySource, BlobSyncUploadFromUriOptions options, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    #endregion
}

