namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class GetEventHubPropertiesConsumerBeforeHookContext(ConsumerEventHubScope scope, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
    : ConsumerBeforeHookContext(scope, ConsumerOperations.GetEventHubProperties, provider, cancellationToken);
