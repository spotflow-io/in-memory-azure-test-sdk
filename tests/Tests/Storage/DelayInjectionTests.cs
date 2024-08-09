using Microsoft.Extensions.Time.Testing;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;

namespace Tests.Storage;

[TestClass]
public class DelayInjectionTests
{
    [TestMethod]
    public async Task Operation_Should_Be_Delayed()
    {
        var timeProvider = new FakeTimeProvider();

        var provider = new InMemoryStorageProvider(timeProvider: timeProvider);

        provider.AddHook(hook => hook.Before(ctx => ctx.DelayAsync(TimeSpan.FromMilliseconds(100))));

        var account = provider.AddAccount("test-account");

        var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test-container");

        var task = Task.Run(() => containerClient.Create());

        while (task.Status != TaskStatus.Running)
        {
            await Task.Delay(10);
        }

        await Task.Delay(1000);

        task.Status.Should().Be(TaskStatus.Running);

        timeProvider.Advance(TimeSpan.FromSeconds(32));

        var response = await task;

        response.Value.LastModified.Should().Be(timeProvider.GetUtcNow());

    }
}
