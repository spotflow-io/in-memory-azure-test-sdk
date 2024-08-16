namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobOpenWriteAfterHookContext(BlobOpenWriteBeforeHookContext before) : BlobAfterHookContext(before)
{
    public BlobOpenWriteBeforeHookContext BeforeContext => before;
}
