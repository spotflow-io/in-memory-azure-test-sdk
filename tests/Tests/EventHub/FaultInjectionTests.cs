using Azure.Messaging.EventHubs;

using Spotflow.InMemory.Azure.EventHubs;

namespace Tests.EventHub;

[TestClass]
public class FaultInjectionTests
{
    [TestMethod]
    public async Task Service_Is_Busy_With_Manual_Fault_Disable_Should_Succeed()
    {
        var provider = new InMemoryEventHubProvider();

        var hook = provider.AddHook(hook => hook.Before(ctx => ctx.Faults().ServiceIsBusy()));

        var eventHub = provider.AddNamespace("test-ns").AddEventHub("test-eh", 1);

        var producerClient = InMemoryEventHubProducerClient.FromEventHub(eventHub);

        var act = () => producerClient.SendAsync([new EventData()]);

        await act.Should().ThrowAsync<EventHubsException>().WithMessage("Event hub 'test-eh' in namespace 'test-ns' is busy. (test-eh). *");

        hook.Disable();

        await act.Should().NotThrowAsync();
    }
}
