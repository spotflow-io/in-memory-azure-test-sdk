namespace Spotflow.InMemory.Azure.EventHubs.Hooks;

public record ConsumerPartitionEventHubScope(string EventHubNamespaceName, string EventHubName, string ConsumerGroup, string PartitionId)
    : ConsumerEventHubScope(EventHubNamespaceName, EventHubName, ConsumerGroup);
