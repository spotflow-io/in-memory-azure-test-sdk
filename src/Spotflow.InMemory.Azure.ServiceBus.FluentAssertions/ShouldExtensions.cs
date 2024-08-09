using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus.FluentAssertions;

public static class ShouldExtensions
{
    public static InMemoryServiceBusTopicSubscriptionAssertions Should(this InMemoryServiceBusSubscription subscription) => new(subscription);

    public static InMemoryServiceBusQueueAssertions Should(this InMemoryServiceBusQueue topic) => new(topic);
}
