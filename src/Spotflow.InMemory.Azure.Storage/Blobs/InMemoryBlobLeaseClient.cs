using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Spotflow.InMemory.Azure.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs;

public class InMemoryBlobLeaseClient : BlobLeaseClient
{
    private readonly InMemoryBlobContainerClient _containerClient;
    private string _leaseId;

    internal InMemoryBlobLeaseClient(InMemoryBlobContainerClient containerClient, string? leaseId)
        : base(containerClient, leaseId)
    {
        _containerClient = containerClient;
        _leaseId = base.LeaseId;
    }

    public override string LeaseId => Volatile.Read(ref _leaseId);

    public override Response<BlobLease> Acquire(TimeSpan duration, RequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateConditions(conditions);

        var container = _containerClient.GetContainerCore();

        if (!container.TryAcquireLease(LeaseId, NormalizeDuration(duration), out var lease, out var error))
        {
            throw error.GetClientException();
        }

        Volatile.Write(ref _leaseId, lease.LeaseId);

        return CreateLeaseResponse(lease, 201);
    }

    public override async Task<Response<BlobLease>> AcquireAsync(TimeSpan duration, RequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Acquire(duration, conditions, cancellationToken);
    }

    public override Response Acquire(TimeSpan duration, RequestConditions conditions, RequestContext context)
    {
        return Acquire(duration, conditions, context?.CancellationToken ?? default).GetRawResponse();
    }

    public override async Task<Response> AcquireAsync(TimeSpan duration, RequestConditions conditions, RequestContext context)
    {
        await Task.Yield();
        return Acquire(duration, conditions, context?.CancellationToken ?? default).GetRawResponse();
    }

    public override Response<BlobLease> Renew(RequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateConditions(conditions);

        var container = _containerClient.GetContainerCore();

        if (!container.TryRenewLease(LeaseId, out var lease, out var error))
        {
            throw error.GetClientException();
        }

        Volatile.Write(ref _leaseId, lease.LeaseId);

        return CreateLeaseResponse(lease, 200);
    }

    public override async Task<Response<BlobLease>> RenewAsync(RequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Renew(conditions, cancellationToken);
    }

    public override Response<BlobLease> Change(string proposedId, RequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateConditions(conditions);

        var container = _containerClient.GetContainerCore();

        if (!container.TryChangeLease(LeaseId, proposedId, out var lease, out var error))
        {
            throw error.GetClientException();
        }

        Volatile.Write(ref _leaseId, lease.LeaseId);

        return CreateLeaseResponse(lease, 200);
    }

    public override async Task<Response<BlobLease>> ChangeAsync(string proposedId, RequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Change(proposedId, conditions, cancellationToken);
    }

    public override Response<ReleasedObjectInfo> Release(RequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateConditions(conditions);

        var container = _containerClient.GetContainerCore();

        if (!container.TryReleaseLease(LeaseId, out var result, out var error))
        {
            throw error.GetClientException();
        }

        return InMemoryResponse.FromValue(
            result,
            200,
            result.ETag,
            new Dictionary<string, string>
            {
                ["Last-Modified"] = result.LastModified.ToString("R")
            });
    }

    public override async Task<Response<ReleasedObjectInfo>> ReleaseAsync(RequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Release(conditions, cancellationToken);
    }

    public override async Task<Response<ReleasedObjectInfo>> ReleaseInternal(RequestConditions conditions, bool async, CancellationToken cancellationToken)
    {
        if (async)
        {
            return await ReleaseAsync(conditions, cancellationToken);
        }

        return Release(conditions, cancellationToken);
    }

    public override Response<BlobLease> Break(TimeSpan? breakPeriod = null, RequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateConditions(conditions);

        var container = _containerClient.GetContainerCore();

        if (!container.TryBreakLease(NormalizeBreakPeriod(breakPeriod), out var result, out var error))
        {
            throw error.GetClientException();
        }

        return InMemoryResponse.FromValue(
            result.Lease,
            202,
            result.Lease.ETag,
            new Dictionary<string, string>
            {
                ["Last-Modified"] = result.Lease.LastModified.ToString("R"),
                ["x-ms-lease-time"] = result.LeaseTime.ToString()
            });
    }

    public override async Task<Response<BlobLease>> BreakAsync(TimeSpan? breakPeriod = null, RequestConditions? conditions = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Break(breakPeriod, conditions, cancellationToken);
    }

    private static TimeSpan NormalizeDuration(TimeSpan duration)
    {
        return duration < TimeSpan.Zero ? InfiniteLeaseDuration : TimeSpan.FromSeconds(Convert.ToInt64(duration.TotalSeconds));
    }

    private static TimeSpan? NormalizeBreakPeriod(TimeSpan? breakPeriod)
    {
        return breakPeriod is null
            ? null
            : TimeSpan.FromSeconds(Convert.ToInt64(breakPeriod.Value.TotalSeconds));
    }

    private static void ValidateConditions(RequestConditions? conditions)
    {
        if (conditions?.IfMatch is not null || conditions?.IfNoneMatch is not null)
        {
            throw new ArgumentException("IfMatch and IfNoneMatch conditions are not supported for container lease operations.", nameof(conditions));
        }
    }

    private static Response<BlobLease> CreateLeaseResponse(BlobLease lease, int status)
    {
        return InMemoryResponse.FromValue(
            lease,
            status,
            lease.ETag,
            new Dictionary<string, string>
            {
                ["Last-Modified"] = lease.LastModified.ToString("R"),
                ["x-ms-lease-id"] = lease.LeaseId
            });
    }
}
