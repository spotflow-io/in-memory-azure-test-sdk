namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class ReceiveBatchBeforeHookContext(PartitionConsumerEventHubScope scope, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
    : PartitionConsumerBeforeHookContext(scope, ConsumerOperations.ReceiveBatch, provider, cancellationToken);
