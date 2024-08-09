namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public abstract class EventHubHookContext(string eventHubNamespaceName, string eventHubName, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
{
    public string EventHubNamespaceName => eventHubNamespaceName;
    public string EventHubName => eventHubName;
    public InMemoryEventHubProvider ResouceProvider => provider;
    public TimeProvider TimeProvider => provider.TimeProvider;
    public CancellationToken CancellationToken => cancellationToken;

    public EventHubFaultsBuilder Faults() => new(this);
}
