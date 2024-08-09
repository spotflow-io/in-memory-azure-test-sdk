namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public class ReceiveBatchBeforeHookContext(ServiceBusConsumerHookScope scope, InMemoryServiceBusProvider provider, CancellationToken cancellationToken)
    : ConsumerBeforeHookContext(scope, ConsumerOperations.ReceiveBatch, provider, cancellationToken);
