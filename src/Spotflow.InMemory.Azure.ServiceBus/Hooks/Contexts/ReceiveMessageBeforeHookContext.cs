namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public class ReceiveMessageBeforeHookContext(ServiceBusConsumerHookScope scope, InMemoryServiceBusProvider provider, CancellationToken cancellationToken)
    : ConsumerBeforeHookContext(scope, ConsumerOperations.ReceiveMessage, provider, cancellationToken);
