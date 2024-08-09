using Azure.Messaging.EventHubs.Producer;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks;

public record ProducerEventHubScope(string EventHubNamespaceName, string EventHubName, string? TargetPartitionKey, string? TargetPartitionId)
    : EventHubScope(EventHubNamespaceName, EventHubName)
{
    public ProducerEventHubScope(EventHubScope eventHubScope, SendEventOptions? options)
        : this(eventHubScope.EventHubNamespaceName, eventHubScope.EventHubName, options?.PartitionKey, options?.PartitionId)
    {
    }
}


