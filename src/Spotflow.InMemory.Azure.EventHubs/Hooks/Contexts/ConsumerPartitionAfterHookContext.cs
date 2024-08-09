using Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public abstract class ConsumerPartitionAfterHookContext(ConsumerPartitionBeforeHookContext before)
    : ConsumerAfterHookContext(before), IConsumerPartitionOperation
{
    public string PartitionId => before.PartitionId;
}
