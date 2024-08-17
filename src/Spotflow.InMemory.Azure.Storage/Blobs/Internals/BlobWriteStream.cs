using Azure;
using Azure.Storage.Blobs.Models;

using Spotflow.InMemory.Azure.Internals;
using Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;

internal class BlobWriteStream(InMemoryBlockBlobClient blobClient, ETag eTag, long? bufferSize) : Stream
{
    private const int _defaultBufferSize = 4 * 1024 * 1024;

    private readonly SemaphoreSlim _syncObj = new(1, 1);
    private readonly List<string> _blocks = [];
    private readonly byte[] _buffer = new byte[bufferSize ?? _defaultBufferSize];
    private readonly BlockBlobStageBlockOptions _stageOptions = new() { Conditions = new() { IfMatch = eTag } };
    private readonly CommitBlockListOptions _commitOptions = new() { Conditions = new() { IfMatch = eTag } };
    private readonly List<IBlobWriteStreamInterceptor> _interceptors = [];

    private int _bufferPosition = 0;
    private long _absolutePosition = 0;


    private bool _isDisposed;

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        await _syncObj.WaitAsync(cancellationToken);

        try
        {
            AssertNotDisposedUnsafe();

            var remainingBuffer = buffer;

            while (remainingBuffer.Length > 0)
            {
                var bytesToCopy = Math.Min(remainingBuffer.Length, _buffer.Length - _bufferPosition);

                remainingBuffer[..bytesToCopy].CopyTo(_buffer.AsMemory(_bufferPosition));

                _bufferPosition += bytesToCopy;

                Interlocked.Add(ref _absolutePosition, bytesToCopy);

                remainingBuffer = remainingBuffer[bytesToCopy..];

                if (_bufferPosition == _buffer.Length)
                {
                    await FlushUnsafeAsync(cancellationToken);
                }
            }

            foreach (var interceptor in _interceptors)
            {
                await interceptor.WriteAsync(buffer, cancellationToken);
            }
        }
        finally
        {
            _syncObj.Release();
        }
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var memory = new ReadOnlyMemory<byte>(buffer, offset, count);
        return WriteAsync(memory, cancellationToken).AsTask();
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var memory = buffer.ToArray().AsMemory();
        WriteAsync(memory).EnsureCompleted();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        var span = new ReadOnlySpan<byte>(buffer, offset, count);
        Write(span);
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();

        await _syncObj.WaitAsync(cancellationToken);
        try
        {
            AssertNotDisposedUnsafe();
            await FlushUnsafeAsync(cancellationToken);
        }
        finally
        {
            _syncObj.Release();
        }
    }

    public override void Flush() => FlushAsync().EnsureCompleted();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeAsync().EnsureCompleted();
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _syncObj.WaitAsync();
        try
        {
            if (!_isDisposed)
            {
                await FlushUnsafeAsync(default);
                blobClient.CommitBlockList(_blocks, options: _commitOptions);

                foreach (var interceptor in _interceptors)
                {
                    await interceptor.DisposeAsync();
                }

                MarkDisposeUnsafe();
            }
        }
        finally
        {
            _syncObj.Release();
        }
    }

    private async Task FlushUnsafeAsync(CancellationToken cancellationToken)
    {
        var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        using var content = new MemoryStream(_buffer, 0, _bufferPosition);

        await blobClient.StageBlockAsync(blockId, content, options: _stageOptions, cancellationToken);

        _blocks.Add(blockId);

        _bufferPosition = 0;

        foreach (var interceptor in _interceptors)
        {
            await interceptor.FlushAsync();
        }

    }

    private void AssertNotDisposedUnsafe() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    private void MarkDisposeUnsafe() => _isDisposed = true;

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => Interlocked.Read(ref _absolutePosition);
        set => throw new NotSupportedException();
    }

    public void AddInterceptor(IBlobWriteStreamInterceptor interceptor)
    {
        _syncObj.Wait();
        try
        {
            AssertNotDisposedUnsafe();
            _interceptors.Add(interceptor);
        }
        finally
        {
            _syncObj.Release();
        }
    }
}
