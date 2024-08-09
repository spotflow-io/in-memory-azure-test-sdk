namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class ReceiveBatchBeforeHookContext(ConsumerEventHubScope scope, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
    : ConsumerBeforeHookContext(scope, ConsumerOperations.ReceiveBatch, provider, cancellationToken);

