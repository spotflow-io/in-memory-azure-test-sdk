namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public interface IBlobWriteStreamInterceptor : IAsyncDisposable
{
    Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);
    Task FlushAsync();
}
