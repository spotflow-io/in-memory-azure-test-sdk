
using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus.Internals;

internal static class ServiceBusConnectionStringUtils
{
    public static string ForNamespace(InMemoryServiceBusNamespace ns)
    {
        return $"Endpoint={ns.FullyQualifiedNamespace};SharedAccessKeyName=<empty>;SharedAccessKey=<empty>";
    }

    public static string GetFullyQualifiedNamespace(string connectionString)
    {
        var properties = ServiceBusConnectionStringProperties.Parse(connectionString);
        return properties.FullyQualifiedNamespace;
    }
}
