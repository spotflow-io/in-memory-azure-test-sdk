using Spotflow.InMemory.Azure.ServiceBus.Hooks.Internals;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public abstract class ProducerBeforeHookContext(ServiceBusProducerHookScope scope, ProducerOperations operation, InMemoryServiceBusProvider provider, CancellationToken cancellationToken)
    : ServiceBusBeforeHookContext(scope, provider, cancellationToken), IProducerOperation
{
    public ProducerOperations Operation => operation;
}

