namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public abstract class EventHubBeforeHookContext(EventHubScope scope, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
    : EventHubHookContext(scope.EventHubNamespaceName, scope.EventHubName, provider, cancellationToken)
{ }
