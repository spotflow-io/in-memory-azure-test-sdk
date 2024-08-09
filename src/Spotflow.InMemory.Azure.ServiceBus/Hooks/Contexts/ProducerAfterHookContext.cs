using Spotflow.InMemory.Azure.ServiceBus.Hooks.Internals;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public abstract class ProducerAfterHookContext(ProducerBeforeHookContext before) : ServiceBusAfterHookContext(before), IProducerOperation
{
    public ProducerOperations Operation => before.Operation;
}

