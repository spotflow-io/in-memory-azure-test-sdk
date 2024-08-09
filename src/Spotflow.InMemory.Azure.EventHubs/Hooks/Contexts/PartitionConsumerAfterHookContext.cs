using Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public abstract class PartitionConsumerAfterHookContext(PartitionConsumerBeforeHookContext before)
    : ConsumerAfterHookContext(before), IConsumerPartitionOperation
{
    public string PartitionId => before.PartitionId;
}
