namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class GetPartitionPropertiesConsumerBeforeHookContext(PartitionConsumerEventHubScope scope, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
    : PartitionConsumerBeforeHookContext(scope, ConsumerOperations.GetPartitionProperties, provider, cancellationToken);
