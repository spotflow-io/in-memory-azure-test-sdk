using Spotflow.InMemory.Azure.Storage.Blobs.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobOpenWriteAfterHookContext : BlobAfterHookContext
{
    internal BlobOpenWriteAfterHookContext(BlobOpenWriteBeforeHookContext beforeContext, BlobWriteStream stream) : base(beforeContext)
    {
        BeforeContext = beforeContext;
        Stream = stream;
    }

    public BlobOpenWriteBeforeHookContext BeforeContext { get; }
    internal BlobWriteStream Stream { get; }

    public void AddStreamInterceptor(IBlobWriteStreamInterceptor interceptor) => Stream.AddInterceptor(interceptor);

}
