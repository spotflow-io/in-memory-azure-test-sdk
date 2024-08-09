using Azure;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Tables;

namespace Tests.Storage.Tables;

[TestClass]
public class FaultInjectionTests
{
    [TestMethod]
    public void Service_Is_Busy_Should_Throw_Exception()
    {
        var provider = new InMemoryStorageProvider();

        provider.AddHook(hook => hook.ForTableService().Before(ctx => ctx.Faults().ServiceIsBusy()));

        var account = provider.AddAccount("test-account");

        var tableClient = InMemoryTableClient.FromAccount(account, "test-container");

        var act = () => tableClient.Create();

        act.Should()
            .Throw<RequestFailedException>()
            .WithMessage("Table service in account 'test-account' is busy.")
            .Where(ex => ex.Status == 503)
            .Where(ex => ex.ErrorCode == "ServerBusy");
    }
}
