using Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public abstract class ConsumerAfterHookContext(ConsumerBeforeHookContext before)
    : EventHubAfterHookContext(before), IConsumerOperation
{
    public ConsumerOperations Operation => before.Operation;

    public string ConsumerGroup => before.ConsumerGroup;

    public string PartitionId => before.PartitionId;
}
