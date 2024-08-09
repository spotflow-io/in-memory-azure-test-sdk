using Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public abstract class ConsumerBeforeHookContext(ConsumerEventHubScope scope, ConsumerOperations operation, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
    : EventHubBeforeHookContext(scope, provider, cancellationToken), IConsumerOperation
{
    public ConsumerOperations Operation => operation;

    public string ConsumerGroup => scope.ConsumerGroup;
}
