using Spotflow.InMemory.Azure.Storage.Hooks;
using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;
using Spotflow.InMemory.Azure.Storage.Tables.Internals;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks;

public class TableServiceFaultsBuilder(StorageHookContext context) : StorageFaultsBuilder
{
    public override Task ServiceIsBusy() => throw TableExceptionFactory.ServiceIsBusy(context.StorageAccountName);
}
