using Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public abstract class PartitionConsumerBeforeHookContext(ConsumerPartitionEventHubScope scope, ConsumerOperations operation, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
    : ConsumerBeforeHookContext(scope, operation, provider, cancellationToken), IConsumerPartitionOperation
{
    public string PartitionId => scope.PartitionId;
}

