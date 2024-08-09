using Azure.Messaging.EventHubs;

using Spotflow.InMemory.Azure.Auth;
using Spotflow.InMemory.Azure.EventHubs.Resources;

namespace Spotflow.InMemory.Azure.EventHubs.Internals;

internal static class EventHubClientUtils
{
    public static InMemoryEventHub GetEventHub(InMemoryEventHubProvider provider, string namespaceHostname, string eventHubName)
    {
        if (!provider.TryGetFullyQualifiedNamespace(namespaceHostname, out var ns))
        {
            throw EventHubExceptionFactory.NamespaceNotFound(namespaceHostname);
        }

        if (!ns.TryGetEventHub(eventHubName, out var eh))
        {
            throw EventHubExceptionFactory.EventHubNotFound(ns, eventHubName);
        }

        return eh;
    }

    public static void HasConsumerGroupOrThrow(InMemoryEventHub eventHub, string consumerGroupName)
    {
        if (!eventHub.HasConsumerGroup(consumerGroupName))
        {
            throw EventHubExceptionFactory.ConsumerGroupNotFound(eventHub, consumerGroupName);
        }
    }

    public static EventHubConnection ConnectionFromConnectionString(string connectionString, string? eventHubName = null)
    {
        if (string.IsNullOrWhiteSpace(eventHubName))
        {
            return new EventHubConnection(connectionString);
        }
        else
        {
            return new EventHubConnection(connectionString, eventHubName);
        }
    }

    public static EventHubConnection Connection(string fullyQualifiedNamespace, string eventHubName)
    {
        return new EventHubConnection(fullyQualifiedNamespace, eventHubName, NoOpTokenCredential.Instance);
    }
}
