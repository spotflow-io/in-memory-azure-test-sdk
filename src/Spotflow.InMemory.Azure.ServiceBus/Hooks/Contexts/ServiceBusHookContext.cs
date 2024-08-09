namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public abstract class ServiceBusHookContext(string serviceBusNamespaceName, string entityPath, InMemoryServiceBusProvider provider, CancellationToken cancellationToken)
{
    public string ServiceBusNamespaceName => serviceBusNamespaceName;
    public string EntityPath => entityPath;
    public CancellationToken CancellationToken => cancellationToken;
    public InMemoryServiceBusProvider ResourceProvider => provider;
    public TimeProvider TimeProvider => provider.TimeProvider;
}
