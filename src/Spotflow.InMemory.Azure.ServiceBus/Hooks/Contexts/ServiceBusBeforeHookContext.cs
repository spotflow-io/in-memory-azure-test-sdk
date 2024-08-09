namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public abstract class ServiceBusBeforeHookContext(ServiceBusHookScope scope, InMemoryServiceBusProvider provider, CancellationToken cancellationToken)
    : ServiceBusHookContext(scope.ServiceBusNamespaceName, scope.EntityPath, provider, cancellationToken);
