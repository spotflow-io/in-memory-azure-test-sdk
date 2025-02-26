
using Azure;
using Azure.Storage.Blobs.Models;

using Spotflow.InMemory.Azure.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;

internal class BlobReadStream : Stream
{
    private readonly object _syncObj = new();

    private readonly int _startPosition;
    private readonly RequestConditions? _conditions;
    private readonly int _initialLength;
    private readonly Func<RequestConditions?, CancellationToken, BinaryData> _fetcher;
    private readonly int _bufferSize;
    private int _currentPosition;
    private bool _isDisposed;

    public BlobReadStream(
        RequestConditions? initialConditions,
        long startPosition,
        BinaryData initialContent,
        BlobProperties initialProperties,
        Func<RequestConditions?, CancellationToken, BinaryData> fetcher,
        bool allowModifications,
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

        _initialLength = initialContent.ToMemory().Length;
        _startPosition = (int) startPosition;
        _currentPosition = _startPosition;
        _fetcher = fetcher;

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
                return _initialLength;
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
                return _currentPosition - _startPosition;
            }
        }

        set => throw new NotSupportedException();
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
    public override bool CanSeek => false;
    public override bool CanWrite => false;

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) => throw BlobExceptionFactory.FeatureNotSupported($"Seeking on the {typeof(BlobReadStream)}");
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
