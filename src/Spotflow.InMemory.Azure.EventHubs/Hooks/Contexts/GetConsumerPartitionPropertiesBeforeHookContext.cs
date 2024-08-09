namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class GetConsumerPartitionPropertiesBeforeHookContext(ConsumerPartitionEventHubScope scope, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
    : ConsumerPartitionBeforeHookContext(scope, ConsumerOperations.GetPartitionProperties, provider, cancellationToken);
