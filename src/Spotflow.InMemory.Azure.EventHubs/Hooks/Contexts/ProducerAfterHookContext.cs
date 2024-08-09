using Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public abstract class ProducerAfterHookContext(ProducerBeforeHookContext before)
    : EventHubAfterHookContext(before), IProducerOperation
{
    public ProducerOperations Operation => before.Operation;
    public string? TargetPartitionId => before.TargetPartitionId;
    public string? TargetPartitionKey => before.TargetPartitionKey;
}
