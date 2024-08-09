using FluentAssertions.Primitives;

using Spotflow.InMemory.Azure.ServiceBus.FluentAssertions.Internal;
using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus.FluentAssertions;

public class InMemoryServiceBusTopicSubscriptionAssertions(InMemoryServiceBusSubscription subject)
    : ReferenceTypeAssertions<InMemoryServiceBusSubscription, InMemoryServiceBusTopicSubscriptionAssertions>(subject)
{
    protected override string Identifier => nameof(InMemoryServiceBusSubscription);

    public async Task BeEmptyAsync(TimeSpan? maxWaitTime = null, string? because = null, params object[] becauseArgs)
    {
        var entity = $"{Subject.Topic.TopicName}/{Subject.SubscriptionName}";

        await ServiceBusAssertionHelpers.EntityShouldBeEmptyAsync(entity, () => Subject.MessageCount, maxWaitTime, because, becauseArgs);
    }
}
