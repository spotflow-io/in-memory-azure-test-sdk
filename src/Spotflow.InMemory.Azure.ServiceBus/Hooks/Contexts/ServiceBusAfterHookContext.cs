namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public abstract class ServiceBusAfterHookContext(ServiceBusBeforeHookContext before)
    : ServiceBusHookContext(before.ServiceBusNamespaceName, before.EntityPath, before.ResourceProvider, before.CancellationToken);

