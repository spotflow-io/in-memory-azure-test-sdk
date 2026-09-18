using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Spotflow.InMemory.Azure.Storage.Blobs.Internals;
using Spotflow.InMemory.Azure.Storage.Resources;

namespace Spotflow.InMemory.Azure.Storage.Blobs;

public class InMemoryBlobBatchClient : BlobBatchClient
{
    private readonly string _accountName;
    private readonly string? _blobContainerName;

    #region Constructors

    public InMemoryBlobBatchClient(InMemoryBlobServiceClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        Uri = client.Uri;
        Provider = client.Provider;

        _accountName = new BlobUriBuilder(client.Uri).AccountName;
        _blobContainerName = null;
    }

    public InMemoryBlobBatchClient(InMemoryBlobContainerClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        Uri = client.Uri;
        Provider = client.Provider;

        _accountName = client.AccountName;
        _blobContainerName = client.Name;
    }

    public static InMemoryBlobBatchClient FromAccount(InMemoryStorageAccount account, bool useConnectionString = false)
    {
        return new(InMemoryBlobServiceClient.FromAccount(account, useConnectionString));
    }

    #endregion

    public override Uri Uri { get; }

    public InMemoryStorageProvider Provider { get; }

    public override Response[] DeleteBlobs(IEnumerable<Uri> blobUris, DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blobUris);

        if (snapshotsOption != DeleteSnapshotsOption.None)
        {
            throw BlobExceptionFactory.FeatureNotSupported(nameof(DeleteSnapshotsOption));
        }

        var blobClients = ResolveBlobClients(blobUris);

        var responses = new Response[blobClients.Count];

        List<Exception>? failures = null;

        // The batch is intentionally not atomic - each sub-request is executed independently, same as in Azure.
        for (var i = 0; i < blobClients.Count; i++)
        {
            try
            {
                responses[i] = blobClients[i].Delete(snapshotsOption, conditions: null, cancellationToken);
            }
            catch (RequestFailedException ex)
            {
                failures ??= [];
                failures.Add(ex);
            }
        }

        if (failures is not null)
        {
            throw new AggregateException("Batch sub-request(s) failed.", failures);
        }

        return responses;
    }

    public override async Task<Response[]> DeleteBlobsAsync(IEnumerable<Uri> blobUris, DeleteSnapshotsOption snapshotsOption = DeleteSnapshotsOption.None, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return DeleteBlobs(blobUris, snapshotsOption, cancellationToken);
    }

    private List<InMemoryBlobClient> ResolveBlobClients(IEnumerable<Uri> blobUris)
    {
        var blobClients = new List<InMemoryBlobClient>();

        foreach (var blobUri in blobUris)
        {
            var blobClient = new InMemoryBlobClient(blobUri, Provider);

            if (!string.Equals(blobClient.AccountName, _accountName, StringComparison.OrdinalIgnoreCase))
            {
                throw BlobExceptionFactory.BatchSubRequestOutOfScope(blobUri, $"account '{_accountName}'");
            }

            if (_blobContainerName is not null && !string.Equals(blobClient.BlobContainerName, _blobContainerName, StringComparison.Ordinal))
            {
                throw BlobExceptionFactory.BatchSubRequestOutOfScope(blobUri, $"container '{_blobContainerName}' in account '{_accountName}'");
            }

            blobClients.Add(blobClient);
        }

        if (blobClients.Count is 0)
        {
            throw new ArgumentException("Cannot submit an empty batch.", nameof(blobUris));
        }

        var limit = InMemoryBlobService.MaxBatchSubRequestCount;

        if (blobClients.Count > limit)
        {
            throw BlobExceptionFactory.BatchTooLarge(limit, blobClients.Count);
        }

        return blobClients;
    }

    #region Unsupported

    public override BlobBatch CreateBatch()
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response SubmitBatch(BlobBatch batch, bool throwOnAnyFailure = false, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> SubmitBatchAsync(BlobBatch batch, bool throwOnAnyFailure = false, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Response[] SetBlobsAccessTier(IEnumerable<Uri> blobUris, AccessTier accessTier, RehydratePriority? rehydratePriority = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    public override Task<Response[]> SetBlobsAccessTierAsync(IEnumerable<Uri> blobUris, AccessTier accessTier, RehydratePriority? rehydratePriority = null, CancellationToken cancellationToken = default)
    {
        throw BlobExceptionFactory.MethodNotSupported();
    }

    #endregion
}
