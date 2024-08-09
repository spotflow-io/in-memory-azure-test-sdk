namespace Spotflow.InMemory.Azure.EventHubs.Hooks;

public record ConsumerEventHubScope(string EventHubNamespaceName, string EventHubName, string ConsumerGroup, string PartitionId)
    : EventHubScope(EventHubNamespaceName, EventHubName);
