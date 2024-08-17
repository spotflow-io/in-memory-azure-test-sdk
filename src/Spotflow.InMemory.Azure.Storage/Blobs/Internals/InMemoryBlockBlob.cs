using System.Diagnostics.CodeAnalysis;

using Azure;
using Azure.Storage.Blobs.Models;

using Spotflow.InMemory.Azure.Storage.Internals;

using static Spotflow.InMemory.Azure.Storage.Blobs.Internals.InMemoryBlockBlob.StageBlockError;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;

internal class InMemoryBlockBlob(string blobName, InMemoryBlobContainer container)
{
    private Dictionary<string, Block>? _uncommittedBlocks = null;
    private List<Block>? _committedBlocks = null;
    private BinaryData? _cachedContent = null;
    private BlobProperties? _properties = null;

    public BlobType BlobType { get; } = BlobType.Block;

    public string Name { get; } = blobName;
    public string ContainerName => Container.Name;
    public string AccountName => Container.AccountName;

    public InMemoryBlobContainer Container { get; } = container;

    public override string? ToString() => $"{Container} / {Name}";

    public bool Exists => _properties is not null;

    public bool HasUncommittedBlocks => _uncommittedBlocks is not null;

    public bool TryGetProperties(
        BlobRequestConditions? conditions,
        [NotNullWhen(true)] out BlobProperties? properties,
        [NotNullWhen(false)] out GetPropertiesError? error)
    {
        if (_properties is null)
        {
            error = new GetPropertiesError.BlobNotFound(this);
            properties = null;
            return false;
        }

        if (!ConditionChecker.CheckConditions(_properties.ETag, conditions?.IfMatch, conditions?.IfNoneMatch, out var conditionError))
        {
            error = new GetPropertiesError.ConditionNotMet(this, conditionError);
            properties = null;
            return false;
        }

        properties = _properties;
        error = null;
        return true;
    }

    public bool TryStageBlock(
        string base64BlockId,
        BinaryData content,
        RequestConditions? conditions,
        [NotNullWhen(true)] out Block? result,
        [NotNullWhen(false)] out StageBlockError? error)
    {

        try
        {
            _ = Convert.FromBase64String(base64BlockId);
        }
        catch (FormatException)
        {
            result = null;
            error = new InvalidBlockId(this, base64BlockId);
            return false;
        }

        if (content.GetLenght() > InMemoryBlobService.MaxBlockSize)
        {
            result = null;
            error = new BlockTooLarge(this, InMemoryBlobService.MaxBlockSize, content.GetLenght());
            return false;
        }

        if (ShouldThrowBlobAlreadyExistsError(conditions))
        {
            error = new BlobAlreadyExists(this);
            result = null;
            return false;
        }

        if (!ConditionChecker.CheckConditions(_properties?.ETag, conditions?.IfMatch, conditions?.IfNoneMatch, out var conditionError))
        {
            result = null;
            error = new ConditionNotMet(this, conditionError);
            return false;
        }

        _uncommittedBlocks ??= [];

        if (_uncommittedBlocks.Count >= InMemoryBlobService.MaxUncommitedBlocks)
        {
            result = null;
            error = new TooManyUncommittedBlocks(this, InMemoryBlobService.MaxUncommitedBlocks, _uncommittedBlocks.Count);
            return false;
        }

        var block = new Block(base64BlockId, content);

        _uncommittedBlocks[base64BlockId] = block;

        result = block;
        error = null;
        return true;

    }

    public bool TryCommitBlockList(
        IEnumerable<string> base64BlockIds,
        RequestConditions? conditions,
        bool? overwrite,
        BlobHttpHeaders? headers,
        IDictionary<string, string>? metadata,
        [NotNullWhen(true)] out BlobProperties? properties,
        [NotNullWhen(false)] out CommitBlockListError? error)
    {


        var canOverwrite = overwrite ?? conditions?.IfNoneMatch != ETag.All;

        if (_committedBlocks is not null && !canOverwrite)
        {
            error = new CommitBlockListError.BlobAlreadyExist(this);
            properties = null;
            return false;
        }

        if (!ConditionChecker.CheckConditions(_properties?.ETag, conditions?.IfMatch, conditions?.IfNoneMatch, out var conditionError))
        {
            properties = null;
            error = new CommitBlockListError.ConditionNotMet(this, conditionError);
            return false;
        }

        IReadOnlyDictionary<string, Block>? currentUncommittedBlocks = _uncommittedBlocks;
        IReadOnlyDictionary<string, Block>? currentCommittedBlocks = _committedBlocks?.ToDictionary(b => b.Id);

        var stagingCommittedBlocks = new List<Block>();

        foreach (var id in base64BlockIds)
        {
            if (currentUncommittedBlocks is not null)
            {
                if (currentUncommittedBlocks.TryGetValue(id, out var block))
                {
                    stagingCommittedBlocks.Add(block);
                    continue;
                }
            }

            if (currentCommittedBlocks is not null)
            {
                if (currentCommittedBlocks.TryGetValue(id, out var block))
                {
                    stagingCommittedBlocks.Add(block);
                    continue;
                }
            }

            error = new CommitBlockListError.BlockNotFound(this, id);
            properties = null;
            return false;
        }

        if (stagingCommittedBlocks.Count > InMemoryBlobService.MaxBlockCount)
        {
            error = new CommitBlockListError.BlockCountExceeded(this, InMemoryBlobService.MaxBlockCount, stagingCommittedBlocks.Count);
            properties = null;
            return false;
        }

        SetCommitedState(headers, metadata, stagingCommittedBlocks);

        error = null;
        properties = _properties;
        return true;

    }

    public bool TryDownload(
        RequestConditions? conditions,
        [NotNullWhen(true)] out BinaryData? content,
        [NotNullWhen(true)] out BlobProperties? properties,
        [NotNullWhen(false)] out DownloadError? error)
    {
        if (_properties is null)
        {
            error = new DownloadError.BlobNotFound(this);
            content = null;
            properties = null;
            return false;
        }

        if (!ConditionChecker.CheckConditions(_properties.ETag, conditions?.IfMatch, conditions?.IfNoneMatch, out var conditionError))
        {
            error = new DownloadError.ConditionNotMet(this, conditionError);
            content = null;
            properties = null;
            return false;
        }

        content = GetContent();
        properties = _properties;
        error = null;
        return true;

    }

    public bool TryGetBlockList(
        BlockListTypes types,
        BlobRequestConditions? conditions,
        [NotNullWhen(true)] out BlockList? blockList,
        [NotNullWhen(false)] out GetBlockListError? error)
    {
        if (_uncommittedBlocks is null && _committedBlocks is null)
        {
            error = new GetBlockListError.BlobNotFound(this);
            blockList = null;
            return false;
        }

        if (!ConditionChecker.CheckConditions(_properties?.ETag, conditions?.IfMatch, conditions?.IfNoneMatch, out var conditionError))
        {
            blockList = null;
            error = new GetBlockListError.ConditionNotMet(this, conditionError);
            return false;
        }

        IEnumerable<BlobBlock>? commitedBlocks = null;
        IEnumerable<BlobBlock>? uncommittedBlocks = null;

        if (types.HasFlag(BlockListTypes.Committed))
        {
            commitedBlocks = _committedBlocks?.Select(b => BlobsModelFactory.BlobBlock(b.Id, b.Content.GetLenght()));
            commitedBlocks ??= Enumerable.Empty<BlobBlock>();
        }

        if (types.HasFlag(BlockListTypes.Uncommitted))
        {
            uncommittedBlocks = _uncommittedBlocks?.Values.Select(b => BlobsModelFactory.BlobBlock(b.Id, b.Content.GetLenght()));
            uncommittedBlocks ??= Enumerable.Empty<BlobBlock>();
        }

        blockList = BlobsModelFactory.BlockList(commitedBlocks, uncommittedBlocks);
        error = null;
        return true;
    }

    public bool TryOpenWrite(RequestConditions? conditions, long? bufferSize, IDictionary<string, string>? metadata, [NotNullWhen(true)] out BlobWriteStream? stream, [NotNullWhen(false)] out OpenWriteError? error)
    {
        if (ShouldThrowBlobAlreadyExistsError(conditions))
        {
            error = new OpenWriteError.BlobAlreadyExists(this);
            stream = null;
            return false;
        }

        if (!ConditionChecker.CheckConditions(_properties?.ETag, conditions?.IfMatch, conditions?.IfNoneMatch, out var conditionError))
        {
            stream = null;
            error = new OpenWriteError.ConditionNotMet(this, conditionError);
            return false;
        }

        SetCommitedState(null, metadata, []);

        var client = InMemoryBlockBlobClient.FromAccount(Container.Service.Account, ContainerName, Name);

        stream = new BlobWriteStream(client, _properties.ETag, bufferSize);
        error = null;
        return true;

    }


    public bool TryDeleteIfExists(BlobRequestConditions? conditions, [NotNullWhen(true)] out bool? deleted, [NotNullWhen(false)] out DeleteError? error)
    {
        if (_properties is null)
        {
            error = null;
            deleted = false;
            return true;
        }

        if (!ConditionChecker.CheckConditions(_properties.ETag, conditions?.IfMatch, conditions?.IfNoneMatch, out var conditionError))
        {
            error = new DeleteError.ConditionNotMet(this, conditionError);
            deleted = null;
            return false;
        }

        DeleteCore();

        error = null;
        deleted = true;

        return true;
    }

    private BinaryData GetContent()
    {
        if (_cachedContent is not null)
        {
            return _cachedContent;
        }

        if (_committedBlocks is null)
        {
            return _cachedContent = new BinaryData(Array.Empty<byte>());
        }

        var len = _committedBlocks.Sum(b => b.Content.GetLenght());

        Memory<byte> buffer = new byte[len];

        var bufferIndex = 0;

        foreach (var block in _committedBlocks)
        {
            block.Content.ToMemory().CopyTo(buffer[bufferIndex..]);
            bufferIndex += block.Content.GetLenght();
        }

        return _cachedContent = new(buffer);
    }

    [MemberNotNull(nameof(_properties))]
    [MemberNotNull(nameof(_committedBlocks))]
    private void SetCommitedState(BlobHttpHeaders? headers, IDictionary<string, string>? metadata, List<Block> committedBlocks)
    {
        var newProperties = BlobsModelFactory.BlobProperties(
            contentLength: _committedBlocks is null ? 0 : GetContent().ToMemory().Length,
            metadata: metadata ?? _properties?.Metadata,
            eTag: new ETag(Guid.NewGuid().ToString()),
            lastModified: DateTimeOffset.UtcNow,
            contentType: headers?.ContentType ?? _properties?.ContentType,
            contentEncoding: headers?.ContentEncoding ?? _properties?.ContentEncoding
            );

        _properties = newProperties;
        _cachedContent = null;
        _uncommittedBlocks = null;
        _committedBlocks = committedBlocks;
    }

    private void DeleteCore()
    {
        _properties = null;
        _committedBlocks = null;
        _uncommittedBlocks = null;
        _cachedContent = null;
    }

    public record Block(string Id, BinaryData Content)
    {
        public BlockInfo GetInfo() => BlobsModelFactory.BlockInfo(null, null, null);
    }

    private bool ShouldThrowBlobAlreadyExistsError(RequestConditions? conditions)
    {
        if (_properties is not null && conditions?.IfNoneMatch == ETag.All)
        {
            return true;
        }

        return false;
    }

    public abstract class CommitBlockListError()
    {
        public abstract RequestFailedException GetClientException();

        public class BlockCountExceeded(InMemoryBlockBlob blob, int Limit, int ActualCount) : CommitBlockListError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.BlockCountExceeded(blob.AccountName, blob.ContainerName, blob.Name, Limit, ActualCount);
        }

        public class BlockNotFound(InMemoryBlockBlob blob, string BlockId) : CommitBlockListError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.BlockNotFound(blob.AccountName, blob.ContainerName, blob.Name, BlockId);
        }

        public class BlobAlreadyExist(InMemoryBlockBlob blob) : CommitBlockListError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.BlobAlreadyExists(blob.AccountName, blob.ContainerName, blob.Name);
        }

        public class ConditionNotMet(InMemoryBlockBlob blob, ConditionError error) : CommitBlockListError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.ConditionNotMet(blob.AccountName, blob.ContainerName, blob.Name, error);
        }



    }

    public abstract class GetBlockListError
    {
        public abstract RequestFailedException GetClientException();

        public class ConditionNotMet(InMemoryBlockBlob blob, ConditionError error) : GetBlockListError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.ConditionNotMet(blob.AccountName, blob.ContainerName, blob.Name, error);
        }

        public class BlobNotFound(InMemoryBlockBlob blob) : GetBlockListError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.BlobNotFound(blob.AccountName, blob.ContainerName, blob.Name);
        }
    }

    public abstract class DeleteError
    {
        public abstract RequestFailedException GetClientException();

        public class ConditionNotMet(InMemoryBlockBlob blob, ConditionError error) : DeleteError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.ConditionNotMet(blob.AccountName, blob.ContainerName, blob.Name, error);
        }
    }

    public abstract class GetPropertiesError
    {
        public abstract RequestFailedException GetClientException();

        public class BlobNotFound(InMemoryBlockBlob blob) : GetPropertiesError
        {
            public override RequestFailedException GetClientException()
            {
                return BlobExceptionFactory.BlobNotFound(blob.AccountName, blob.ContainerName, blob.Name);
            }
        }

        public class ConditionNotMet(InMemoryBlockBlob blob, ConditionError error) : GetPropertiesError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.ConditionNotMet(blob.AccountName, blob.ContainerName, blob.Name, error);
        }
    }

    public abstract class DownloadError
    {
        public abstract RequestFailedException GetClientException();

        public class BlobNotFound(InMemoryBlockBlob blob) : DownloadError
        {
            public override RequestFailedException GetClientException()
            {
                return BlobExceptionFactory.BlobNotFound(blob.AccountName, blob.ContainerName, blob.Name);
            }
        }

        public class ConditionNotMet(InMemoryBlockBlob blob, ConditionError error) : DownloadError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.ConditionNotMet(blob.AccountName, blob.ContainerName, blob.Name, error);
        }
    }

    public abstract class OpenWriteError
    {
        public abstract RequestFailedException GetClientException();

        public class ConditionNotMet(InMemoryBlockBlob blob, ConditionError error) : OpenWriteError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.ConditionNotMet(blob.AccountName, blob.ContainerName, blob.Name, error);
        }

        public class BlobAlreadyExists(InMemoryBlockBlob blob) : OpenWriteError
        {
            public override RequestFailedException GetClientException()
            {
                return BlobExceptionFactory.BlobAlreadyExists(blob.AccountName, blob.ContainerName, blob.Name);
            }
        }
    }

    public abstract class StageBlockError
    {
        public abstract RequestFailedException GetClientException();

        public class BlobAlreadyExists(InMemoryBlockBlob blob) : StageBlockError
        {
            public override RequestFailedException GetClientException()
            {
                return BlobExceptionFactory.BlobAlreadyExists(blob.AccountName, blob.ContainerName, blob.Name);
            }
        }

        public class TooManyUncommittedBlocks(InMemoryBlockBlob blob, int limit, int actualCount) : StageBlockError
        {
            public override RequestFailedException GetClientException()
            {
                return BlobExceptionFactory.TooManyUncommittedBlocks(blob.AccountName, blob.ContainerName, blob.Name, limit, actualCount);
            }
        }
        public class BlockTooLarge(InMemoryBlockBlob blob, int limit, int actualSize) : StageBlockError
        {
            public override RequestFailedException GetClientException()
            {
                return BlobExceptionFactory.BlockTooLarge(blob.AccountName, blob.ContainerName, blob.Name, limit, actualSize);
            }
        }
        public class ConditionNotMet(InMemoryBlockBlob blob, ConditionError error) : StageBlockError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.ConditionNotMet(blob.AccountName, blob.ContainerName, blob.Name, error);
        }

        public class InvalidBlockId(InMemoryBlockBlob blob, string actualValue) : StageBlockError
        {
            public override RequestFailedException GetClientException()
            {
                return BlobExceptionFactory.InvalidQueryParameterValue(
                    blob.AccountName,
                    blob.ContainerName,
                    blob.Name,
                    "blockid",
                    actualValue,
                    "Not a valid base64 string."
                    );
            }
        }
    }
}
