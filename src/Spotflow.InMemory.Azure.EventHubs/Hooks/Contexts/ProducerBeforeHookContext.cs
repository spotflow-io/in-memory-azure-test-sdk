using Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public abstract class ProducerBeforeHookContext(ProducerEventHubScope scope, ProducerOperations operation, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
    : EventHubBeforeHookContext(scope, provider, cancellationToken), IProducerOperation
{
    public ProducerOperations Operation => operation;
    public string? TargetPartitionId => scope.TargetPartitionId;
    public string? TargetPartitionKey => scope.TargetPartitionKey;
}
