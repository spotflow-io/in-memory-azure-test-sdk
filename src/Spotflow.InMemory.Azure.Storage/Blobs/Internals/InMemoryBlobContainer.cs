using System.Diagnostics.CodeAnalysis;

using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Spotflow.InMemory.Azure.Storage.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;

internal class InMemoryBlobContainer(string name, IDictionary<string, string>? metadata, InMemoryBlobService service)
{
    private readonly TimeProvider _timeProvider = service.Account.Provider.TimeProvider;

    private readonly object _lock = new();
    private readonly SortedDictionary<string, BlobEntry> _blobEntries = new(StringComparer.Ordinal);

    private readonly BlobContainerProperties _properties = BlobsModelFactory.BlobContainerProperties(
            lastModified: service.Account.Provider.TimeProvider.GetUtcNow(),
            eTag: new ETag($"\"{Guid.NewGuid()}\""),
            metadata: metadata);

    private LeaseData? _lease;

    public string Name { get; } = name;

    public string AccountName => Service.Account.Name;

    public BlobContainerProperties GetProperties()
    {
        lock (_lock)
        {
            return GetPropertiesCore(GetLeaseSnapshot());
        }
    }

    public bool TryGetProperties(
        string? leaseId,
        [NotNullWhen(true)] out BlobContainerProperties? properties,
        [NotNullWhen(false)] out ContainerOperationError? error)
    {
        lock (_lock)
        {
            var lease = GetLeaseSnapshot();

            if (!TryValidateLeaseIdForContainerOperation(leaseId, lease, out error))
            {
                properties = null;
                return false;
            }

            properties = GetPropertiesCore(lease);
            return true;
        }
    }

    public bool TryAcquireLease(
        string leaseId,
        TimeSpan duration,
        [NotNullWhen(true)] out BlobLease? result,
        [NotNullWhen(false)] out LeaseError? error)
    {
        lock (_lock)
        {
            if (!Guid.TryParse(leaseId, out _))
            {
                result = null;
                error = new LeaseError.InvalidLeaseId();
                return false;
            }

            if (duration != BlobLeaseClient.InfiniteLeaseDuration &&
                (duration < TimeSpan.FromSeconds(15) || duration > TimeSpan.FromSeconds(60)))
            {
                result = null;
                error = new LeaseError.InvalidLeaseDuration();
                return false;
            }

            var lease = GetLeaseSnapshot();

            if (lease.State == LeaseState.Breaking)
            {
                result = null;
                error = new LeaseError.LeaseIsBreakingAndCannotBeAcquired();
                return false;
            }

            if (lease.State == LeaseState.Leased && _lease?.Id != leaseId)
            {
                result = null;
                error = new LeaseError.LeaseAlreadyPresent();
                return false;
            }

            _lease = new(
                leaseId,
                duration,
                duration == BlobLeaseClient.InfiniteLeaseDuration
                    ? null
                    : _timeProvider.GetUtcNow() + duration,
                BreaksOn: null);

            result = CreateLease(leaseId);
            error = null;
            return true;
        }
    }

    public bool TryRenewLease(
        string leaseId,
        [NotNullWhen(true)] out BlobLease? result,
        [NotNullWhen(false)] out LeaseError? error)
    {
        lock (_lock)
        {
            var lease = GetLeaseSnapshot();

            if (!Guid.TryParse(leaseId, out _))
            {
                result = null;
                error = new LeaseError.InvalidLeaseId();
                return false;
            }

            if (_lease is not { } leaseData)
            {
                result = null;
                error = new LeaseError.LeaseNotPresent();
                return false;
            }

            if (leaseData.Id != leaseId)
            {
                result = null;
                error = new LeaseError.LeaseIdMismatch();
                return false;
            }

            if (lease.State == LeaseState.Breaking)
            {
                result = null;
                error = new LeaseError.LeaseIsBreakingAndCannotBeRenewed();
                return false;
            }

            if (lease.State == LeaseState.Broken)
            {
                result = null;
                error = new LeaseError.LeaseIsBrokenAndCannotBeRenewed();
                return false;
            }

            if (leaseData.Duration != BlobLeaseClient.InfiniteLeaseDuration)
            {
                _lease = leaseData with { ExpiresOn = _timeProvider.GetUtcNow() + leaseData.Duration };
            }

            result = CreateLease(leaseId);
            error = null;
            return true;
        }
    }

    public bool TryChangeLease(
        string leaseId,
        string proposedId,
        [NotNullWhen(true)] out BlobLease? result,
        [NotNullWhen(false)] out LeaseError? error)
    {
        lock (_lock)
        {
            if (!Guid.TryParse(leaseId, out _))
            {
                result = null;
                error = new LeaseError.InvalidLeaseId();
                return false;
            }

            if (!Guid.TryParse(proposedId, out _))
            {
                result = null;
                error = new LeaseError.InvalidLeaseId();
                return false;
            }

            var lease = GetLeaseSnapshot();

            if (_lease is not { } leaseData)
            {
                result = null;
                error = new LeaseError.LeaseNotPresent();
                return false;
            }

            if (leaseData.Id != leaseId)
            {
                result = null;
                error = new LeaseError.LeaseIdMismatch();
                return false;
            }

            if (lease.State == LeaseState.Breaking)
            {
                result = null;
                error = new LeaseError.LeaseIsBreakingAndCannotBeChanged();
                return false;
            }

            if (lease.State != LeaseState.Leased)
            {
                result = null;
                error = new LeaseError.LeaseNotPresent();
                return false;
            }

            _lease = leaseData with { Id = proposedId };
            result = CreateLease(proposedId);
            error = null;
            return true;
        }
    }

    public bool TryReleaseLease(
        string leaseId,
        [NotNullWhen(true)] out ReleasedObjectInfo? result,
        [NotNullWhen(false)] out LeaseError? error)
    {
        lock (_lock)
        {
            if (!Guid.TryParse(leaseId, out _))
            {
                result = null;
                error = new LeaseError.InvalidLeaseId();
                return false;
            }

            if (_lease is not { } leaseData)
            {
                result = null;
                error = new LeaseError.LeaseNotPresent();
                return false;
            }

            if (leaseData.Id != leaseId)
            {
                result = null;
                error = new LeaseError.LeaseIdMismatch();
                return false;
            }

            _lease = default;

            result = new ReleasedObjectInfo(_properties.ETag, _properties.LastModified);
            error = null;
            return true;
        }
    }

    public bool TryBreakLease(
        TimeSpan? breakPeriod,
        [NotNullWhen(true)] out BreakLeaseResult? result,
        [NotNullWhen(false)] out LeaseError? error)
    {
        lock (_lock)
        {
            if (breakPeriod < TimeSpan.Zero || breakPeriod > TimeSpan.FromSeconds(60))
            {
                result = null;
                error = new LeaseError.InvalidLeaseBreakPeriod();
                return false;
            }

            var lease = GetLeaseSnapshot();

            if (_lease is not { } leaseData)
            {
                result = null;
                error = new LeaseError.LeaseNotPresent();
                return false;
            }

            var now = _timeProvider.GetUtcNow();
            var remaining = lease.State switch
            {
                LeaseState.Leased when leaseData.ExpiresOn is not null => leaseData.ExpiresOn.Value - now,
                LeaseState.Breaking => leaseData.BreaksOn!.Value - now,
                _ => TimeSpan.Zero
            };

            var requestedPeriod = breakPeriod ?? (
                leaseData.Duration == BlobLeaseClient.InfiniteLeaseDuration ? TimeSpan.Zero : remaining);
            var actualPeriod = lease.State is LeaseState.Broken or LeaseState.Expired
                ? TimeSpan.Zero
                : remaining > TimeSpan.Zero && requestedPeriod > remaining
                    ? remaining
                    : requestedPeriod;

            _lease = leaseData with { BreaksOn = now + actualPeriod };

            result = new(CreateLease(null), (int) Math.Ceiling(actualPeriod.TotalSeconds));
            error = null;
            return true;
        }
    }

    public bool TryValidateDelete(BlobRequestConditions? conditions, [NotNullWhen(false)] out ContainerOperationError? error)
    {
        lock (_lock)
        {
            var lease = GetLeaseSnapshot();
            var leaseId = conditions?.LeaseId;

            if (lease.State is LeaseState.Leased or LeaseState.Breaking)
            {
                if (leaseId is null)
                {
                    error = new ContainerOperationError.LeaseIdMissing();
                    return false;
                }

                if (!Guid.TryParse(leaseId, out _))
                {
                    error = new ContainerOperationError.InvalidLeaseId();
                    return false;
                }

                if (_lease?.Id != leaseId)
                {
                    error = new ContainerOperationError.LeaseIdMismatch();
                    return false;
                }
            }
            else if (leaseId is not null)
            {
                if (!Guid.TryParse(leaseId, out _))
                {
                    error = new ContainerOperationError.InvalidLeaseId();
                    return false;
                }

                if (lease.State == LeaseState.Expired && _lease?.Id == leaseId)
                {
                    error = new ContainerOperationError.LeaseLost();
                    return false;
                }

                error = new ContainerOperationError.LeaseNotPresent();
                return false;
            }

            if (!ConditionChecker.CheckConditions(_properties.ETag, conditions?.IfMatch, conditions?.IfNoneMatch, out var conditionError))
            {
                error = new ContainerOperationError.ConditionNotMet(this, conditionError);
                return false;
            }

            error = null;
            return true;
        }
    }

    private bool TryValidateLeaseIdForContainerOperation(
        string? leaseId,
        LeaseSnapshot lease,
        [NotNullWhen(false)] out ContainerOperationError? error)
    {
        if (leaseId is null)
        {
            error = null;
            return true;
        }

        if (lease.State is LeaseState.Leased or LeaseState.Breaking)
        {
            if (_lease?.Id != leaseId)
            {
                error = new ContainerOperationError.LeaseIdMismatch();
                return false;
            }

            error = null;
            return true;
        }

        if (lease.State == LeaseState.Expired && _lease?.Id == leaseId)
        {
            error = new ContainerOperationError.LeaseLost();
            return false;
        }

        error = new ContainerOperationError.LeaseNotPresent();
        return false;
    }

    private BlobContainerProperties GetPropertiesCore(LeaseSnapshot lease)
    {
        return BlobsModelFactory.BlobContainerProperties(
            lastModified: _properties.LastModified,
            eTag: _properties.ETag,
            leaseState: lease.State,
            leaseDuration: lease.Duration,
            leaseStatus: lease.Status,
            metadata: _properties.Metadata);
    }

    private BlobLease CreateLease(string? leaseId)
    {
        return BlobsModelFactory.BlobLease(_properties.ETag, _properties.LastModified, leaseId);
    }

    private LeaseSnapshot GetLeaseSnapshot()
    {
        if (_lease is not { } leaseData)
        {
            return new(LeaseState.Available, LeaseStatus.Unlocked, null);
        }

        var now = _timeProvider.GetUtcNow();
        if (leaseData.BreaksOn is not null)
        {
            return leaseData.BreaksOn > now
                ? new(LeaseState.Breaking, LeaseStatus.Locked, null)
                : new(LeaseState.Broken, LeaseStatus.Unlocked, null);
        }

        if (leaseData.ExpiresOn is not null && leaseData.ExpiresOn <= now)
        {
            return new(LeaseState.Expired, LeaseStatus.Unlocked, null);
        }

        var duration = leaseData.Duration == BlobLeaseClient.InfiniteLeaseDuration
            ? LeaseDurationType.Infinite
            : LeaseDurationType.Fixed;

        return new(LeaseState.Leased, LeaseStatus.Locked, duration);
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

    private record LeaseSnapshot(LeaseState State, LeaseStatus Status, LeaseDurationType? Duration);

    public record BreakLeaseResult(BlobLease Lease, int LeaseTime);

    public abstract class LeaseError
    {
        public abstract RequestFailedException GetClientException();

        public class InvalidLeaseId : LeaseError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.InvalidLeaseId();
        }

        public class InvalidLeaseDuration : LeaseError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.InvalidLeaseDuration();
        }

        public class InvalidLeaseBreakPeriod : LeaseError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.InvalidLeaseBreakPeriod();
        }

        public class LeaseAlreadyPresent : LeaseError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.LeaseAlreadyPresent();
        }

        public class LeaseNotPresent : LeaseError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.LeaseNotPresent();
        }

        public class LeaseIdMismatch : LeaseError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.LeaseIdMismatch();
        }

        public class LeaseIsBreakingAndCannotBeAcquired : LeaseError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.LeaseIsBreakingAndCannotBeAcquired();
        }

        public class LeaseIsBreakingAndCannotBeRenewed : LeaseError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.LeaseIsBreakingAndCannotBeRenewed();
        }

        public class LeaseIsBreakingAndCannotBeChanged : LeaseError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.LeaseIsBreakingAndCannotBeChanged();
        }

        public class LeaseIsBrokenAndCannotBeRenewed : LeaseError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.LeaseIsBrokenAndCannotBeRenewed();
        }
    }

    public abstract class ContainerOperationError
    {
        public abstract RequestFailedException GetClientException();

        public class InvalidLeaseId : ContainerOperationError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.InvalidLeaseId();
        }

        public class LeaseIdMissing : ContainerOperationError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.LeaseIdMissing();
        }

        public class LeaseIdMismatch : ContainerOperationError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.LeaseIdMismatchWithContainerOperation();
        }

        public class LeaseLost : ContainerOperationError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.LeaseLost();
        }

        public class LeaseNotPresent : ContainerOperationError
        {
            public override RequestFailedException GetClientException() => BlobExceptionFactory.LeaseNotPresentWithContainerOperation();
        }

        public class ConditionNotMet(InMemoryBlobContainer container, ConditionError error) : ContainerOperationError
        {
            public override RequestFailedException GetClientException()
                => BlobExceptionFactory.ConditionNotMet(error.ConditionType, container.AccountName, container.Name, error.Message);
        }
    }

    private readonly record struct LeaseData(
        string Id,
        TimeSpan Duration,
        DateTimeOffset? ExpiresOn,
        DateTimeOffset? BreaksOn);

}
