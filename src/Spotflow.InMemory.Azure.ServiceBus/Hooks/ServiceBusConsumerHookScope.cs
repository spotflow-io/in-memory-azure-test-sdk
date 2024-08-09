namespace Spotflow.InMemory.Azure.ServiceBus.Hooks;

public record ServiceBusConsumerHookScope(
    string ServiceBusNamespaceName,
    string EntityPath,
    bool IsTopicSubscription) : ServiceBusHookScope(ServiceBusNamespaceName, EntityPath);
