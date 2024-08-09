namespace Spotflow.InMemory.Azure.ServiceBus.Hooks;

public record ServiceBusProducerHookScope(string ServiceBusNamespaceName, string EntityPath) : ServiceBusHookScope(ServiceBusNamespaceName, EntityPath);
