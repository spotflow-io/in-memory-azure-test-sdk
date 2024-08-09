using Spotflow.InMemory.Azure.Storage.Blobs.Internals;
using Spotflow.InMemory.Azure.Storage.Hooks;
using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks;

public class BlobServiceFaultsBuilder(StorageHookContext context) : StorageFaultsBuilder
{
    public Task AuthenticationFailedSignatureDidNotMatch()
    {
        throw BlobExceptionFactory.AuthenticationFailedSignatureDidNotMatch(context.StorageAccountName);
    }

    public override Task ServiceIsBusy() => throw BlobExceptionFactory.ServiceIsBusy(context.StorageAccountName);
}


