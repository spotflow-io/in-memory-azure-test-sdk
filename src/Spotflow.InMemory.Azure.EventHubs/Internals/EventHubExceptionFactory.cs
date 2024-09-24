using System.Net.Sockets;
using System.Runtime.CompilerServices;

using Azure.Messaging.EventHubs;

using Spotflow.InMemory.Azure.EventHubs.Resources;

namespace Spotflow.InMemory.Azure.EventHubs.Internals;

internal static class EventHubExceptionFactory
{
    public static NotSupportedException MethodNotSupported([CallerMemberName] string? callerMemberName = null)
    {
        return new($"In-memory event hub client does not support method '{callerMemberName}'.");
    }

    public static NotSupportedException FeatureNotSupported(string featureName)
    {
        return new($"In-memory event hub client does not support feature '{featureName}'.");
    }

    public static EventHubsException ConsumerGroupNotFound(InMemoryEventHub eventHub, string consumerGroupName)
    {
        return ResourceNotFound(consumerGroupName, $"Consumer Group '{consumerGroupName}' not found in '{eventHub}'.");
    }

    public static EventHubsException EventHubNotFound(InMemoryEventHubNamespace @namespace, string eventHubName)
    {
        return ResourceNotFound(eventHubName, $"Event Hub '{eventHubName}' not found in '{@namespace}'.");
    }

    public static SocketException NamespaceNotFound(string namespaceHostname)
    {
        return new SocketException(11001, $"No such host is known: {namespaceHostname}");
    }

    public static EventHubsException PartitionNotFound(InMemoryEventHub eh, string partitionId)
    {
        return ResourceNotFound(eh.Properties.Name, $"Partition '{partitionId}' not found in '{eh}'.");
    }

    private static EventHubsException ResourceIsBusy(string? eventHubName, string message)
    {
        return new(true, eventHubName, message, EventHubsException.FailureReason.ServiceBusy);
    }

    private static EventHubsException ResourceNotFound(string eventHubName, string message)
    {
        return new(false, eventHubName, message, EventHubsException.FailureReason.ResourceNotFound);
    }

    public static EventHubsException ServiceIsBusy(string namespaceHostname, string eventHubName, string? partitionId)
    {
        if (partitionId is not null)
        {
            return ResourceIsBusy(
                eventHubName,
                $"Partition '{partitionId}' in event hub '{eventHubName}' in namespace '{namespaceHostname}' is busy.");
        }
        else
        {
            return ResourceIsBusy(
                eventHubName,
                $"Event hub '{eventHubName}' in namespace '{namespaceHostname}' is busy.");
        }
    }

    public static ArgumentException InvalidStartingSequenceNumber(InMemoryEventPosition supplied, long last)
    {
        var message = $"" +
            $"The supplied sequence number '{supplied.SequenceNumber}' is invalid. " +
            $"The last sequence number in the system is '{last}'. " +
            $"Is supplied sequence number inclusive = {supplied.IsInclusive}.";

        return new(message);
    }
}
