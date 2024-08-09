using Spotflow.InMemory.Azure.ServiceBus.Hooks.Internals;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public abstract class ConsumerBeforeHookContext(ServiceBusConsumerHookScope scope, ConsumerOperations operation, InMemoryServiceBusProvider provider, CancellationToken cancellationToken)
    : ServiceBusBeforeHookContext(scope, provider, cancellationToken), IConsumerOperation
{
    public ConsumerOperations Operation => operation;
    public bool IsTopicSubscription => scope.IsTopicSubscription;
}

