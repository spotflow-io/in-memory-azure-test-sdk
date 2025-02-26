using Azure;
using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;

internal class InMemoryBlobContainer(string name, IDictionary<string, string>? metadata, InMemoryBlobService service)
{
    private readonly TimeProvider _timeProvider = service.Account.Provider.TimeProvider;

    private readonly object _lock = new();
    private readonly SortedDictionary<string, BlobEntry> _blobEntries = new(StringComparer.Ordinal);

    private readonly BlobContainerProperties _properties = BlobsModelFactory.BlobContainerProperties(
            lastModified: service.Account.Provider.TimeProvider.GetUtcNow(),
            eTag: new ETag(Guid.NewGuid().ToString()),
            metadata: metadata);

    public string Name { get; } = name;

    public string AccountName => Service.Account.Name;

    public BlobContainerProperties GetProperties()
    {
        lock (_lock)
        {
            return _properties;
        }
    }

    public InMemoryBlobService Service { get; } = service;

    public override string? ToString() => $"{Service} / {Name}";

    public IReadOnlyList<BlobItem> GetBlobs(
        string? prefix,
        bool includeMetadata,
        bool includeUncommittedBlobs)
    {
        lock (_lock)
        {
            return _blobEntries
                .Values
                .Where(entry => filter(entry.Blob))
                .Select(createBlobItem)
                .ToList();
        }

        bool filter(InMemoryBlockBlob blob)
        {
            var result = true;

            result &= blob.Exists || (includeUncommittedBlobs && blob.HasUncommittedBlocks);
            result &= prefix is null || blob.Name.StartsWith(prefix);

            return result;
        }

        BlobItem createBlobItem(BlobEntry entry)
        {
            IDictionary<string, string>? metadata = null;
            BlobItemProperties? itemProperties = null;

            if (includeMetadata)
            {
                if (entry.Blob.Exists)
                {
                    if (!entry.Blob.TryGetProperties(null, out var properties, out var error))
                    {
                        throw new InvalidOperationException("Since blob exists and we don't use any conditions properties should be returned without problem.");
                    }

                    metadata = properties.Metadata;

                    itemProperties = BlobsModelFactory.BlobItemProperties(
                        accessTierInferred: false,
                        contentType: properties.ContentType,
                        // Empty contentEncoding is represented here as an empty string but in GetProperties* methods it's represented as null
                        contentEncoding: properties.ContentEncoding ?? string.Empty,
                        contentLength: properties.ContentLength,
                        lastModified: properties.LastModified,
                        eTag: properties.ETag,
                        createdOn: properties.CreatedOn
                    );
                }
                else
                {
                    metadata = new Dictionary<string, string>();
                    itemProperties = BlobsModelFactory.BlobItemProperties(
                        accessTierInferred: false,
                        contentType: null,
                        contentEncoding: null,
                        contentLength: 0,
                        lastModified: new DateTimeOffset(),
                        eTag: new ETag(),
                        createdOn: null
                    );
                }
            }

            return BlobsModelFactory.BlobItem(
                entry.Blob.Name,
                properties: itemProperties,
                metadata: metadata);
        }
    }

    public AcquiredBlob AcquireBlob(string blobName, CancellationToken cancellationToken)
    {
        var entry = GetBlobEntry(blobName);

        entry.Semaphore.Wait(cancellationToken);

        return new(entry.Blob, entry.Semaphore);
    }

    private BlobEntry GetBlobEntry(string blobName)
    {
        BlobEntry? entry;

        lock (_lock)
        {
            if (!_blobEntries.TryGetValue(blobName, out entry))
            {
                var blob = new InMemoryBlockBlob(blobName, this, _timeProvider);
                entry = new(blob, new(1, 1));
                _blobEntries.Add(blobName, entry);
            }
        }

        return entry;
    }

    public sealed class AcquiredBlob(InMemoryBlockBlob blob, SemaphoreSlim semaphore) : IDisposable
    {
        public InMemoryBlockBlob Value { get; } = blob ?? throw new ArgumentNullException(nameof(blob));

        public void Dispose() => semaphore.Release();
    }

    private record BlobEntry(InMemoryBlockBlob Blob, SemaphoreSlim Semaphore);

}

