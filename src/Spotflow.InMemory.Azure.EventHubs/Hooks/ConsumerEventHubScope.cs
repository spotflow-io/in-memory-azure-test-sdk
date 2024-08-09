
namespace Spotflow.InMemory.Azure.EventHubs.Hooks;

public record ConsumerEventHubScope(string EventHubNamespaceName, string EventHubName, string ConsumerGroup)
    : EventHubScope(EventHubNamespaceName, EventHubName)
{
    public ConsumerPartitionEventHubScope WithPartition(string partitionId) => new(EventHubNamespaceName, EventHubName, ConsumerGroup, partitionId);
}
