namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class GetPartitionPropertiesConsumerBeforeHookContext(ConsumerPartitionEventHubScope scope, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
    : PartitionConsumerBeforeHookContext(scope, ConsumerOperations.GetPartitionProperties, provider, cancellationToken);
