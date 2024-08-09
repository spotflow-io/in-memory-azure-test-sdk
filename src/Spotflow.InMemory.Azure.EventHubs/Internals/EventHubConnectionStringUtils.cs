using System.Security.Cryptography;
using System.Text;

using Spotflow.InMemory.Azure.EventHubs.Resources;

namespace Spotflow.InMemory.Azure.EventHubs.Internals;

internal static class EventHubConnectionStringUtils
{
    public static string ForEventHub(InMemoryEventHub eventHub, string? keyName = null)
    {
        var namespaceConnectionString = ForNamespace(eventHub.Namespace, keyName).TrimEnd(';');
        return $"{namespaceConnectionString};EntityPath={eventHub.Name};";
    }

    public static string ForNamespace(InMemoryEventHubNamespace ns, string? keyName = null)
    {
        keyName ??= "test-key";

        var keySeed = $"{keyName}|{ns.FullyQualifiedNamespace}";

        var key = Convert.ToBase64String(SHA384.HashData(Encoding.UTF8.GetBytes(keySeed)));

        return $"Endpoint=sb://{ns.FullyQualifiedNamespace};SharedAccessKey={key};SharedAccessKeyName={keyName};";
    }

}
