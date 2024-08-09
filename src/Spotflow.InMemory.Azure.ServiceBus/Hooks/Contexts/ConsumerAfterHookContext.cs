using Spotflow.InMemory.Azure.ServiceBus.Hooks.Internals;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public abstract class ConsumerAfterHookContext(ConsumerBeforeHookContext before) : ServiceBusAfterHookContext(before), IConsumerOperation
{
    public ConsumerOperations Operation => before.Operation;
    public bool IsTopicSubscription => before.IsTopicSubscription;
}

