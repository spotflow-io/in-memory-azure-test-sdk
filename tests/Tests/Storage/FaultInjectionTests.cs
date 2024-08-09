using Azure;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;
using Spotflow.InMemory.Azure.Storage.Tables;

namespace Tests.Storage;


[TestClass]
public class FaultInjectionTests
{
    [TestMethod]
    public void Service_Is_Busy_With_Manual_Fault_Disable_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var hook = provider.AddHook(hook => hook.Before(ctx => ctx.Faults().ServiceIsBusy()));

        var account = provider.AddAccount("test-account");

        var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test-container");
        var tableClient = InMemoryTableClient.FromAccount(account, "test-table");

        var actBlob = () => containerClient.Create();
        var actTable = () => tableClient.Create();

        actBlob.Should().Throw<RequestFailedException>().WithMessage("Blob service in account 'test-account' is busy.");
        actTable.Should().Throw<RequestFailedException>().WithMessage("Table service in account 'test-account' is busy.");

        hook.Disable();

        actBlob.Should().NotThrow();
        actTable.Should().NotThrow();


    }

    [TestMethod]
    public void Service_Is_Busy_For_Specific_Account_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account1 = provider.AddAccount("test-account-1");
        var account2 = provider.AddAccount("test-account-2");

        var hook = provider.AddHook(hook => hook.Before(ctx => ctx.Faults().ServiceIsBusy(), storageAccountName: account1.Name));

        var actBlob1 = () => InMemoryBlobContainerClient.FromAccount(account1, "test-container").Create();
        var actTable1 = () => InMemoryTableClient.FromAccount(account1, "test-table").Create();

        var actBlob2 = () => InMemoryBlobContainerClient.FromAccount(account2, "test-container").Create();
        var actTable2 = () => InMemoryTableClient.FromAccount(account2, "test-table").Create();

        actBlob1.Should().Throw<RequestFailedException>();
        actTable1.Should().Throw<RequestFailedException>();

        actBlob2.Should().NotThrow();
        actTable2.Should().NotThrow();
    }

    [TestMethod]
    public void Service_Is_Busy_With_Automatic_Fault_Disable_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var hook = provider.AddHook(hook => hook.Before(ctx => ctx.Faults().ServiceIsBusy()));

        hook.DisableAfter(invocationCount: 1);

        var account = provider.AddAccount("test-account");

        var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test-container");

        var act = () => containerClient.Create();

        act.Should().Throw<RequestFailedException>().WithMessage("Blob service in account 'test-account' is busy.");

        act.Should().NotThrow();
    }
}
