using FluentAssertions.Primitives;

using Spotflow.InMemory.Azure.ServiceBus.FluentAssertions.Internal;
using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus.FluentAssertions;

public class InMemoryServiceBusQueueAssertions(InMemoryServiceBusQueue subject)
    : ReferenceTypeAssertions<InMemoryServiceBusQueue, InMemoryServiceBusQueueAssertions>(subject)
{
    protected override string Identifier => nameof(InMemoryServiceBusQueue);

    public async Task BeEmptyAsync(TimeSpan? maxWaitTime = null, string? because = null, params object[] becauseArgs)
    {
        var entity = $"{Subject.QueueName}";

        await ServiceBusAssertionHelpers.EntityShouldBeEmptyAsync(entity, () => Subject.MessageCount, maxWaitTime, because, becauseArgs);
    }
}
