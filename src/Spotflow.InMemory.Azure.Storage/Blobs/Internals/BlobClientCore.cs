using System.Diagnostics.CodeAnalysis;
using System.IO;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Spotflow.InMemory.Azure.Internals;
using Spotflow.InMemory.Azure.Storage.Blobs.Hooks;
using Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;

internal class BlobClientCore(BlobUriBuilder uriBuilder, InMemoryStorageProvider provider)
{
    public Uri Uri { get; } = uriBuilder.ToUri();
    public string AccountName { get; } = uriBuilder.AccountName;
    public string BlobContainerName { get; } = uriBuilder.BlobContainerName;
    public string Name { get; } = uriBuilder.BlobName;
    public InMemoryStorageProvider Provider { get; } = provider ?? throw new ArgumentNullException(nameof(provider));

    private readonly BlobScope _scope = new(uriBuilder.AccountName, uriBuilder.BlobContainerName, uriBuilder.BlobName);

    public async Task<Response<BlobDownloadInfo>> DownloadAsync(BlobDownloadOptions? options, CancellationToken cancellationToken)
    {
        var (info, partialContent) = await DownloadStreamingCoreAsync(options, cancellationToken);

        return InMemoryResponse.FromValue(info, partialContent ? 206 : 200);
    }

    public async Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(BlobDownloadOptions? options, CancellationToken cancellationToken)
    {
        var (info, partialContent) = await DownloadStreamingCoreAsync(options, cancellationToken);

        return InMemoryResponse.FromValue(
            BlobsModelFactory.BlobDownloadStreamingResult(info.Content, info.Details),
            partialContent ? 206 : 200);
    }

    private async Task<(BlobDownloadInfo Info, bool PartialContent)> DownloadStreamingCoreAsync(BlobDownloadOptions? options, CancellationToken cancellationToken)
    {
        var (content, properties, partialContent) = await DownloadCoreAsync(options, cancellationToken);
        var getContent = (RequestConditions? conditions, CancellationToken cancellationToken) =>
        {
            var streamContent = GetContent(conditions, cancellationToken);
            var sliceResult = SliceContentIfNeeded(streamContent, options?.Range);

            if (sliceResult is SliceContentResult.Sliced sliced)
            {
                streamContent = sliced.Data;
            }
            else if (sliceResult is SliceContentResult.InvalidRange invalidRange)
            {
                throw invalidRange.GetClientException();
            }

            return streamContent;
        };
        var stream = new BlobReadStream(options?.Conditions, 0, content, properties, getContent, allowModifications: false);
        var info = GetDownloadInfo(content, properties, stream);

        return (info, partialContent);
    }

    public async Task<Response<BlobDownloadResult>> DownloadContentAsync(BlobDownloadOptions? options, CancellationToken cancellationToken)
    {
        var (content, properties, partialContent) = await DownloadCoreAsync(options, cancellationToken);

        var details = GetDownloadInfo(content, properties, null).Details;

        return InMemoryResponse.FromValue(
            BlobsModelFactory.BlobDownloadResult(content, details),
            partialContent ? 206 : 200);
    }

    private async Task<DownloadCoreResult> DownloadCoreAsync(BlobDownloadOptions? options, CancellationToken cancellationToken)
    {
        var beforeContext = new BlobDownloadBeforeHookContext(_scope, Provider, cancellationToken)
        {
            Options = options
        };

        await ExecuteBeforeHooksAsync(beforeContext).ConfigureAwait(ConfigureAwaitOptions.None);

        var (content, properties) = GetContentWithProperties(options?.Conditions, cancellationToken);

        var partialContent = false;
        var sliceResult = SliceContentIfNeeded(content, options?.Range);

        if (sliceResult is SliceContentResult.Sliced sliced)
        {
            content = sliced.Data;
            partialContent = true;
        }
        else if (sliceResult is SliceContentResult.InvalidRange invalidRange)
        {
            throw invalidRange.GetClientException();
        }

        var afterContext = new BlobDownloadAfterHookContext(beforeContext)
        {
            BlobProperties = properties,
            Content = content
        };

        await ExecuteAfterHooksAsync(afterContext).ConfigureAwait(ConfigureAwaitOptions.None);

        return new(content, properties, partialContent);
    }

    public BlobProperties GetProperties(BlobRequestConditions? conditions, CancellationToken cancellationToken)
    {
        using var blob = AcquireBlob(cancellationToken);

        if (!blob.Value.TryGetProperties(conditions, out var properties, out var error))
        {
            throw error.GetClientException();
        }

        return properties;
    }

    public bool Exists(CancellationToken cancellationToken)
    {
        using var blob = AcquireBlob(cancellationToken);

        return blob.Value.Exists;
    }

    public BlockList GetBlockList(BlockListTypes types, BlobRequestConditions? conditions, CancellationToken cancellationToken)
    {
        using var blob = AcquireBlob(cancellationToken);

        if (!blob.Value.TryGetBlockList(types, conditions, out var blockList, out var error))
        {
            throw error.GetClientException();
        }

        return blockList;

    }

    public async Task<BlobContentInfo> UploadAsync(BinaryData content, BlobUploadOptions? options, bool? overwrite, CancellationToken cancellationToken)
    {
        var beforeContext = new BlobUploadBeforeHookContext(_scope, Provider, cancellationToken)
        {
            Content = content,
            Options = options
        };

        await ExecuteBeforeHooksAsync(beforeContext).ConfigureAwait(ConfigureAwaitOptions.None);

        RequestConditions? conditions = options?.Conditions;

        var contentMemory = content.ToMemory();

        var index = 0;

        var blockList = new List<string>();

        while (index < contentMemory.Length)
        {
            var blockSize = Math.Min(contentMemory.Length - index, InMemoryBlobService.MaxBlockSize);

            var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            var block = new BinaryData(contentMemory[index..blockSize]);

            using (var blob = AcquireBlob(cancellationToken))
            {
                if (!blob.Value.TryStageBlock(blockId, block, conditions, out _, out var error))
                {
                    throw error.GetClientException();
                }
            }

            blockList.Add(blockId);
            index += blockSize;
        }

        using var blobToCommit = AcquireBlob(cancellationToken);

        var result = CommitBlockListCoreUnsafe(blockList, blobToCommit.Value, conditions, overwrite, options?.HttpHeaders, options?.Metadata);

        var afterContext = new BlobUploadAfterHookContext(beforeContext)
        {
            BlobContentInfo = result,
            Content = content
        };

        await ExecuteAfterHooksAsync(afterContext).ConfigureAwait(ConfigureAwaitOptions.None);

        return result;
    }



    public BlobContentInfo CommitBlockList(IEnumerable<string> blockIds, CommitBlockListOptions? options, CancellationToken cancellationToken)
    {
        RequestConditions? conditions = options?.Conditions;

        using var blob = AcquireBlob(cancellationToken);

        return CommitBlockListCoreUnsafe(blockIds, blob.Value, conditions, null, options?.HttpHeaders, options?.Metadata);
    }


    public BlockInfo StageBlock(string blockId, BinaryData content, BlockBlobStageBlockOptions? options, CancellationToken cancellationToken)
    {
        RequestConditions? conditions = options?.Conditions;

        using var blob = AcquireBlob(cancellationToken);

        if (!blob.Value.TryStageBlock(blockId, content, conditions, out var block, out var stageError))
        {
            throw stageError.GetClientException();
        }

        return block.GetInfo();
    }

    public async Task<Stream> OpenWriteAsync(bool overwrite, BlobOpenWriteOptions? options, CancellationToken cancellationToken)
    {
        var beforeContext = new BlobOpenWriteBeforeHookContext(_scope, Provider, cancellationToken)
        {
            Options = options
        };

        await ExecuteBeforeHooksAsync(beforeContext);

        if (!overwrite)
        {
            throw new ArgumentException("BlockBlobClient.OpenWrite only supports overwriting");
        }

        using var blob = AcquireBlob(cancellationToken);

        if (!blob.Value.TryOpenWrite(options?.OpenConditions, options?.BufferSize, options?.Metadata, out var stream, out var error))
        {
            throw error.GetClientException();
        }

        var afterContext = new BlobOpenWriteAfterHookContext(beforeContext, stream);

        await ExecuteAfterHooksAsync(afterContext);

        return stream;
    }

    public Response Delete(DeleteSnapshotsOption snapshotsOption, BlobRequestConditions? conditions, CancellationToken cancellationToken)
    {
        if (snapshotsOption != DeleteSnapshotsOption.None)
        {
            throw BlobExceptionFactory.FeatureNotSupported(nameof(DeleteSnapshotsOption));
        }

        using var blob = AcquireBlob(cancellationToken);

        if (!blob.Value.TryDeleteIfExists(conditions, out var deleted, out var error))
        {
            throw error.GetClientException();
        }

        if (!deleted.Value)
        {
            throw BlobExceptionFactory.BlobNotFound(AccountName, BlobContainerName, Name);
        }

        return new InMemoryResponse(202);

    }

    public Response<bool> DeleteIfExists(DeleteSnapshotsOption snapshotsOption, BlobRequestConditions? conditions, CancellationToken cancellationToken)
    {

        if (snapshotsOption != DeleteSnapshotsOption.None)
        {
            throw BlobExceptionFactory.FeatureNotSupported(nameof(DeleteSnapshotsOption));
        }

        using var blob = AcquireBlob(cancellationToken);

        if (!blob.Value.TryDeleteIfExists(conditions, out var deleted, out var error))
        {
            throw error.GetClientException();
        }

        if (deleted.Value)
        {
            return InMemoryResponse.FromValue(true, 202);
        }
        else
        {
            return Response.FromValue(false, null!);
        }
    }


    public BlobContainerClient GetParentContainerClient()
    {
        var containerUriBuilder = new BlobUriBuilder(Uri)
        {
            BlobName = null
        };

        return new InMemoryBlobContainerClient(containerUriBuilder.ToUri(), Provider);
    }


    public async Task<Stream> OpenReadAsync(BlobOpenReadOptions options, CancellationToken cancellationToken)
    {
        var beforeContext = new BlobOpenReadBeforeHookContext(_scope, Provider, cancellationToken)
        {
            Options = options
        };

        await ExecuteBeforeHooksAsync(beforeContext);

        var (content, properties) = GetContentWithProperties(options.Conditions, cancellationToken);

        var allowModifications = ReflectionUtils.ReadInternalValueProperty<bool>(options, "AllowModifications");

        var stream = new BlobReadStream(options.Conditions, options.Position, content, properties, GetContent, allowModifications, options.BufferSize);

        var afterContext = new BlobOpenReadAfterHookContext(beforeContext)
        {
            BlobProperties = properties,
            Content = content
        };

        await ExecuteAfterHooksAsync(afterContext);

        return stream;
    }

    public BlockInfo StageBlockFromUri(Uri sourceUri, string base64BlockId, StageBlockFromUriOptions? options, CancellationToken cancellationToken)
    {
        var sourceUriBuilder = new BlobUriBuilder(sourceUri);
        var sourceClient = new BlobClientCore(sourceUriBuilder, Provider);

        if (!sourceClient.TryAcquireBlob(out var sourceBlob, out var acquireError, cancellationToken))
        {
            throw acquireError switch
            {
                AcquireBlobError.BlobServiceNotFound => BlobExceptionFactory.SourceBlobServiceNotFound(),
                AcquireBlobError.ContainerNotFound => BlobExceptionFactory.SourceBlobContainerNotFound(),
                _ => throw new InvalidOperationException("Unexpected error")
            };
        }

        using (sourceBlob)
        {
            if (!sourceBlob.Value.TryDownload(
                    options?.SourceConditions.IfMatch,
                    options?.SourceConditions.IfNoneMatch,
                    out var sourceContent,
                    out _,
                    out var downloadError))
            {
                throw downloadError switch
                {
                    InMemoryBlockBlob.DownloadError.BlobNotFound => BlobExceptionFactory.SourceBlobNotFound(),
                    InMemoryBlockBlob.DownloadError.ConditionNotMet { Error: { ConditionType: Storage.Internals.ConditionType.IfMatch } } =>
                        BlobExceptionFactory.SourceBlobIfMatchFailed(),
                    InMemoryBlockBlob.DownloadError.ConditionNotMet { Error: { ConditionType: Storage.Internals.ConditionType.IfNoneMatch } } =>
                        BlobExceptionFactory.SourceBlobIfNoneMatchFailed(),
                    _ => throw new InvalidOperationException("Unexpected error")
                };
            }

            var stageOptions = new BlockBlobStageBlockOptions
            {
                Conditions = options?.DestinationConditions ?? new BlobRequestConditions(),
            };

            return StageBlock(base64BlockId, sourceContent, stageOptions, cancellationToken);
        }
    }

    private BlobContentWithProperties GetContentWithProperties(RequestConditions? conditions, CancellationToken cancellationToken)
    {
        using var blob = AcquireBlob(cancellationToken);

        if (!blob.Value.TryDownload(conditions?.IfMatch, conditions?.IfNoneMatch, out var content, out var properties, out var error))
        {
            throw error.GetClientException();
        }

        return new(content, properties);
    }

    private static SliceContentResult SliceContentIfNeeded(BinaryData data, HttpRange? range)
    {
        if (range is null || range is { Offset: 0, Length: null })
        {
            return new SliceContentResult.NotNeeded();
        }

        var bytes = data.ToMemory();

        if (range.Value.Offset > (bytes.Length - 1))
        {
            return new SliceContentResult.InvalidRange();
        }

        bytes = data.ToMemory()[(int) range.Value.Offset..];

        if (range.Value.Length is not null)
        {
            bytes = bytes[..Math.Min((int) range.Value.Length.Value, bytes.Length)];
        }

        return new SliceContentResult.Sliced(BinaryData.FromBytes(bytes));
    }

    private BinaryData GetContent(RequestConditions? conditions, CancellationToken cancellationToken)
    {
        return GetContentWithProperties(conditions, cancellationToken).Content;
    }

    private static BlobDownloadInfo GetDownloadInfo(BinaryData content, BlobProperties properties, Stream? contentStream)
    {
        return BlobsModelFactory.BlobDownloadInfo(
            blobType: BlobType.Block,
            contentLength: content.ToMemory().Length,
            eTag: properties.ETag,
            lastModified: properties.LastModified,
            content: contentStream
            );
    }


    private static BlobContentInfo CommitBlockListCoreUnsafe(
        IEnumerable<string> blockIds,
        InMemoryBlockBlob blob,
        RequestConditions? conditions,
        bool? overwrite,
        BlobHttpHeaders? headers,
        IDictionary<string, string>? metadata)
    {
        if (!blob.TryCommitBlockList(blockIds, conditions, overwrite, headers, metadata, out var properties, out var error))
        {
            throw error.GetClientException();
        }

        return BlobsModelFactory.BlobContentInfo(properties.ETag, properties.LastModified, default, default, default, default, default);
    }

    private InMemoryBlobContainer.AcquiredBlob AcquireBlob(CancellationToken cancellationToken)
    {
        if (!TryAcquireBlob(out var blob, out var error, cancellationToken))
        {
            throw error.GetClientException();
        }

        return blob;
    }

    private bool TryAcquireBlob(
        [NotNullWhen(true)] out InMemoryBlobContainer.AcquiredBlob? blob,
        [NotNullWhen(false)] out AcquireBlobError? error,
        CancellationToken cancellationToken)
    {
        if (!Provider.TryGetAccount(AccountName, out var account))
        {
            error = new AcquireBlobError.BlobServiceNotFound(AccountName, Provider);
            blob = null;
            return false;
        }

        if (!account.BlobService.TryGetBlobContainer(BlobContainerName, out var container))
        {
            error = new AcquireBlobError.ContainerNotFound(BlobContainerName, account.BlobService);
            blob = null;
            return false;
        }

        blob = container.AcquireBlob(Name, cancellationToken);
        error = null;
        return true;
    }

    private Task ExecuteBeforeHooksAsync<TContext>(TContext context) where TContext : BlobBeforeHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

    private Task ExecuteAfterHooksAsync<TContext>(TContext context) where TContext : BlobAfterHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

    private abstract class AcquireBlobError
    {
        public abstract Exception GetClientException();

        public class BlobServiceNotFound(string accountName, InMemoryStorageProvider provider) : AcquireBlobError
        {
            public override Exception GetClientException() =>
                BlobExceptionFactory.BlobServiceNotFound(accountName, provider);
        }

        public class ContainerNotFound(string containerName, InMemoryBlobService service) : AcquireBlobError
        {
            public override Exception GetClientException() =>
                BlobExceptionFactory.ContainerNotFound(containerName, service);
        }
    }

    private abstract class SliceContentResult
    {
        public class NotNeeded() : SliceContentResult;

        public class Sliced(BinaryData data) : SliceContentResult
        {
            public BinaryData Data { get; } = data;
        }

        public class InvalidRange() : SliceContentResult
        {
            public Exception GetClientException() => BlobExceptionFactory.InvalidRange();
        }
    }

    private record DownloadCoreResult(BinaryData Content, BlobProperties Properties, bool partialContent);
}
