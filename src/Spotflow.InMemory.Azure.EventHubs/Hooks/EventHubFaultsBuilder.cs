using Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;
using Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;
using Spotflow.InMemory.Azure.EventHubs.Internals;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks;

public class EventHubFaultsBuilder(EventHubHookContext context)
{
    public Task ServiceIsBusy()
    {
        string? partitionId = null;

        if (context is IConsumerPartitionOperation consumer)
        {
            partitionId = consumer.PartitionId;
        }

        throw EventHubExceptionFactory.ServiceIsBusy(context.EventHubNamespaceName, context.EventHubName, partitionId);
    }
}

