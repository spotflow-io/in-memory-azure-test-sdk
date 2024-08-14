using Azure;
using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;


internal class BlobWriteStream(InMemoryBlockBlobClient blobClient, ETag eTag, long? bufferSize) : Stream
{
    private const int _defaultBufferSize = 4 * 1024 * 1024;

    private readonly object _syncObj = new();
    private readonly List<string> _blocks = [];
    private readonly byte[] _buffer = new byte[bufferSize ?? _defaultBufferSize];
    private readonly BlockBlobStageBlockOptions _stageOptions = new() { Conditions = new() { IfMatch = eTag } };
    private readonly CommitBlockListOptions _commitOptions = new() { Conditions = new() { IfMatch = eTag } };

    private int _bufferPosition = 0;
    private bool _isDisposed;

    public override void Write(byte[] buffer, int offset, int count)
    {
        lock (_syncObj)
        {
            AssertNotDisposedUnsafe();
            var remainingBytesToCopy = count;

            while (remainingBytesToCopy > 0)
            {
                var bytesToCopy = Math.Min(remainingBytesToCopy, _buffer.Length - _bufferPosition);

                Array.Copy(buffer, offset, _buffer, _bufferPosition, bytesToCopy);

                _bufferPosition += bytesToCopy;
                offset += bytesToCopy;
                remainingBytesToCopy -= bytesToCopy;

                if (_bufferPosition == _buffer.Length)
                {
                    FlushUnsafe();
                }
            }
        }
    }

    public override void Flush()
    {
        lock (_syncObj)
        {
            AssertNotDisposedUnsafe();
            FlushUnsafe();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_syncObj)
            {
                if (!_isDisposed)
                {
                    FlushUnsafe();
                    blobClient.CommitBlockList(_blocks, options: _commitOptions);
                    MarkDisposeUnsafe();
                }
            }

        }

        base.Dispose(disposing);
    }

    private void FlushUnsafe()
    {
        var blockId = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        var content = new MemoryStream(_buffer, 0, _bufferPosition);

        blobClient.StageBlock(blockId, content, options: _stageOptions);

        _blocks.Add(blockId);

        _bufferPosition = 0;
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

    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }


}
