
using Azure;
using Azure.Storage.Blobs.Models;

using Spotflow.InMemory.Azure.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;

internal class BlobReadStream : Stream
{
    private readonly object _syncObj = new();

    private readonly RequestConditions? _conditions;
    private readonly Func<RequestConditions?, CancellationToken, BinaryData> _fetcher;
    private readonly bool _allowModifications;
    private readonly bool _canSeek;
    private readonly int _bufferSize;
    private int _length;
    private int _currentPosition;
    private bool _isDisposed;

    public BlobReadStream(
        RequestConditions? initialConditions,
        long startPosition,
        BinaryData initialContent,
        BlobProperties initialProperties,
        Func<RequestConditions?, CancellationToken, BinaryData> fetcher,
        bool allowModifications,
        bool canSeek,
        int? bufferSize = null
        )
    {
        ArgumentOutOfRangeException.ThrowIfNegative(startPosition);

        if (bufferSize is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize.Value);
            _bufferSize = bufferSize.Value;
        }
        else
        {
            _bufferSize = 1024;
        }

        _length = initialContent.ToMemory().Length;
        _currentPosition = (int) startPosition;
        _fetcher = fetcher;
        _allowModifications = allowModifications;
        _canSeek = canSeek;

        _conditions = new()
        {
            IfMatch = allowModifications ? null : initialProperties.ETag,
            IfNoneMatch = initialConditions?.IfNoneMatch,
            IfModifiedSince = initialConditions?.IfModifiedSince,
            IfUnmodifiedSince = initialConditions?.IfUnmodifiedSince
        };
    }

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var content = _fetcher(_conditions, cancellationToken).ToMemory();

        lock (_syncObj)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            if (_allowModifications)
            {
                // The blob can grow or shrink while it is being read so the last known length must be kept up-to-date.
                _length = content.Length;
            }

            var remainingBytes = content.Length - _currentPosition;

            if (remainingBytes <= 0)
            {
                return 0;
            }

            var bytesToCopy = Math.Min(remainingBytes, buffer.Length);
            bytesToCopy = Math.Min(bytesToCopy, _bufferSize);

            content.Slice(_currentPosition, bytesToCopy).CopyTo(buffer);

            _currentPosition += bytesToCopy;

            return bytesToCopy;
        }
    }

    public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count).EnsureCompleted();

    public override long Length
    {
        get
        {
            lock (_syncObj)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                return _length;
            }
        }
    }

    public override long Position
    {
        get
        {

            lock (_syncObj)
            {
                ObjectDisposedException.ThrowIf(_isDisposed, this);
                return _currentPosition;
            }
        }

        set => Seek(value, SeekOrigin.Begin);
    }

    protected override void Dispose(bool disposing)
    {
        lock (_syncObj)
        {
            _isDisposed = true;
        }

        base.Dispose(disposing);
    }

    public override bool CanRead => true;
    public override bool CanSeek => _canSeek;
    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (!_canSeek)
        {
            throw BlobExceptionFactory.FeatureNotSupported($"Seeking on the {typeof(BlobReadStream)}");
        }

        lock (_syncObj)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            var newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _currentPosition + offset,
                SeekOrigin.End when _allowModifications => throw new ArgumentException(
                    $"Cannot {nameof(Seek)} with {nameof(SeekOrigin)}.{nameof(SeekOrigin.End)} on a growing blob or file. " +
                    $"Call Stream.Seek(Stream.Length, SeekOrigin.Begin) to get to the end of known data.", nameof(origin)),
                SeekOrigin.End => _length + offset,
                _ => throw new ArgumentException($"Unknown ${nameof(SeekOrigin)} value", nameof(origin))
            };

            if (newPosition == _currentPosition)
            {
                return _currentPosition;
            }

            if (newPosition < 0)
            {
                throw new ArgumentException($"New {nameof(offset)} cannot be less than 0.  Value was {newPosition}", nameof(offset));
            }

            if (newPosition > _length)
            {
                throw new ArgumentException("You cannot seek past the last known length of the underlying blob or file.", nameof(offset));
            }

            _currentPosition = (int) newPosition;

            return _currentPosition;
        }
    }

    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
