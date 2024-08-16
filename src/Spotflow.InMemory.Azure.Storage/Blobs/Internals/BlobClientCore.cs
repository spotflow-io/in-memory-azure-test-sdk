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

    public async Task<BlobDownloadInfo> DownloadAsync(BlobDownloadOptions? options, CancellationToken cancellationToken)
    {
        var (content, properties) = await DownloadCoreAsync(options, cancellationToken);
        var stream = new BlobReadStream(options?.Conditions, 0, content, properties, GetContent, allowModifications: false);
        return GetDownloadInfo(content, properties, stream);
    }

    public async Task<BlobDownloadStreamingResult> DownloadStreamingAsync(BlobDownloadOptions? options, CancellationToken cancellationToken)
    {
        var info = await DownloadAsync(options, cancellationToken);
        return BlobsModelFactory.BlobDownloadStreamingResult(info.Content, info.Details);
    }

    public async Task<BlobDownloadResult> DownloadContentAsync(BlobDownloadOptions? options, CancellationToken cancellationToken)
    {
        var (content, properties) = await DownloadCoreAsync(options, cancellationToken);

        var details = GetDownloadInfo(content, properties, null).Details;

        return BlobsModelFactory.BlobDownloadResult(content, details);
    }

    private async Task<BlobContentWithProperties> DownloadCoreAsync(BlobDownloadOptions? options, CancellationToken cancellationToken)
    {
        var beforeContext = new BlobDownloadBeforeHookContext(_scope, Provider, cancellationToken)
        {
            Options = options
        };

        await ExecuteBeforeHooksAsync(beforeContext).ConfigureAwait(ConfigureAwaitOptions.None);

        var (content, properties) = GetContentWithProperties(options?.Conditions, cancellationToken);

        var afterContext = new BlobDownloadAfterHookContext(beforeContext)
        {
            BlobProperties = properties,
            Content = content
        };

        await ExecuteAfterHooksAsync(afterContext).ConfigureAwait(ConfigureAwaitOptions.None);

        return new(content, properties);
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

        if (!blob.Value.TryOpenWrite(options?.OpenConditions, options?.BufferSize, out var stream, out var error))
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

    private BlobContentWithProperties GetContentWithProperties(RequestConditions? conditions, CancellationToken cancellationToken)
    {
        using var blob = AcquireBlob(cancellationToken);

        if (!blob.Value.TryDownload(conditions, out var content, out var properties, out var error))
        {
            throw error.GetClientException();
        }

        return new(content, properties);
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
        if (!Provider.TryGetAccount(AccountName, out var account))
        {
            throw BlobExceptionFactory.BlobServiceNotFound(AccountName, Provider);
        }

        if (!account.BlobService.TryGetBlobContainer(BlobContainerName, out var container))
        {
            throw BlobExceptionFactory.ContainerNotFound(BlobContainerName, account.BlobService);
        }

        return container.AcquireBlob(Name, cancellationToken);
    }

    private Task ExecuteBeforeHooksAsync<TContext>(TContext context) where TContext : BlobBeforeHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

    private Task ExecuteAfterHooksAsync<TContext>(TContext context) where TContext : BlobAfterHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }


}


