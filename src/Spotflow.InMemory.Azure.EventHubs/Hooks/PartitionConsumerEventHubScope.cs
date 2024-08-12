namespace Spotflow.InMemory.Azure.EventHubs.Hooks;

public record PartitionConsumerEventHubScope(string EventHubNamespaceName, string EventHubName, string ConsumerGroup, string PartitionId)
    : ConsumerEventHubScope(EventHubNamespaceName, EventHubName, ConsumerGroup);
