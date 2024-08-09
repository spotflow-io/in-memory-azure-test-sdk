namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class ReceiveBatchBeforeHookContext(ConsumerPartitionEventHubScope scope, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
    : ConsumerPartitionBeforeHookContext(scope, ConsumerOperations.ReceiveBatch, provider, cancellationToken);
