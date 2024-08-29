using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Azure.Messaging.EventHubs;

using Spotflow.InMemory.Azure.EventHubs.Internals;

namespace Spotflow.InMemory.Azure.EventHubs.Resources;

public class InMemoryEventHubNamespace
{
    private readonly ConcurrentDictionary<string, InMemoryEventHub> _eventHubs = new();

    public InMemoryEventHubNamespace(string name, InMemoryEventHubProvider provider)
    {
        FullyQualifiedNamespace = $"{name}.{provider.HostnameSuffix.TrimStart('.')}";
        Name = name;
        Provider = provider;
    }

    public string FullyQualifiedNamespace { get; }
    public string Name { get; }

    public InMemoryEventHubProvider Provider { get; }

    public string CreateConnectionString() => EventHubConnectionStringUtils.ForNamespace(this);

    public InMemoryEventHub AddEventHub(string eventHubName, int numberOfPartitions, InMemoryPartitionInitialState? partitionInitialState = null)
    {
        if (numberOfPartitions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(numberOfPartitions), numberOfPartitions, "Number of partitions must be greater than 0.");
        }

        var partitionIds = Enumerable.Range(0, numberOfPartitions).Select(id => id.ToString()).ToArray();
        var properties = EventHubsModelFactory.EventHubProperties(eventHubName, DateTimeOffset.UtcNow, partitionIds);

        var options = new InMemoryEventHubOptions();


        var eventHub = new InMemoryEventHub(eventHubName, properties, partitionInitialState, this);

        if (!_eventHubs.TryAdd(eventHubName, eventHub))
        {
            throw new InvalidOperationException($"Event Hub '{eventHubName}' already exists in namespace '{Name}'.");
        }

        return eventHub;

    }

    public InMemoryEventHub GetEventHub(string eventHubName)
    {
        if (!TryGetEventHub(eventHubName, out var eh))
        {
            throw new InvalidOperationException($"Event Hub '{eventHubName}' not found in namespace '{Name}'.");
        }

        return eh;
    }

    public override string ToString() => $"{Name} ({FullyQualifiedNamespace})";

    public bool TryGetEventHub(string eventHubName, [NotNullWhen(true)] out InMemoryEventHub? eventHub)
    {
        return _eventHubs.TryGetValue(eventHubName, out eventHub);
    }
}
