using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Azure.Messaging.EventHubs;

using Spotflow.InMemory.Azure.EventHubs.Internals;
namespace Spotflow.InMemory.Azure.EventHubs.Resources;

public class InMemoryEventHub
{
    public const string DefaultConsumerGroupName = "$Default";

    private readonly IReadOnlyDictionary<string, InMemoryPartition> _partitions;
    private readonly object _roundRobinPartitionLock = new();
    private readonly ConcurrentDictionary<string, byte> _consumerGroups;
    private int _roundRobinPartitionIndex = 0;

    public InMemoryEventHub(
        string name,
        EventHubProperties properties,
        InMemoryPartitionInitialState? partitionInitialState,
        InMemoryEventHubNamespace @namespace)
    {
        Namespace = @namespace;
        Name = name;
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _consumerGroups = new(StringComparer.OrdinalIgnoreCase);
        _consumerGroups[DefaultConsumerGroupName] = 1;
        _partitions = CreatePartitions(Properties.PartitionIds, partitionInitialState, this);
    }

    private static IReadOnlyDictionary<string, InMemoryPartition> CreatePartitions(string[] partitionIds, InMemoryPartitionInitialState? partitionInitialState, InMemoryEventHub parent)
    {
        var result = new Dictionary<string, InMemoryPartition>();

        foreach (var id in partitionIds)
        {
            result[id] = new(id, partitionInitialState, parent);
        }

        return result;
    }

    public InMemoryEventHubProvider Provider => Namespace.Provider;
    public InMemoryEventHubNamespace Namespace { get; }
    public EventHubProperties Properties { get; }

    public IReadOnlySet<string> ConsumerGroups => _consumerGroups.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
    public string Name { get; }

    public bool HasConsumerGroup(string consumerGroupName) => _consumerGroups.ContainsKey(consumerGroupName);

    public string CreateConnectionString() => EventHubConnectionStringUtils.ForEventHub(this);

    public override string ToString() => $"{Name} [{Namespace}]";

    public InMemoryEventHub AddConsumerGroup(string consumerGroupName)
    {
        if (!_consumerGroups.TryAdd(consumerGroupName, 1))
        {
            throw new InvalidOperationException($"Consumer group '{consumerGroupName}' alredy exists in event hub '{Name}' in namespace {Namespace.Name}.");
        }

        return this;
    }

    public void TriggerRetentionPolicy(string partitionId, int deleteCount)
    {
        var partition = GetPartition(partitionId);
        partition.TriggerRetentionPolicy(deleteCount);
    }

    internal InMemoryPartition GetRoundRobinPartition()
    {
        int index;

        lock (_roundRobinPartitionLock)
        {
            if (_roundRobinPartitionIndex == _partitions.Count)
            {
                index = _roundRobinPartitionIndex = 0;
            }
            else
            {
                index = _roundRobinPartitionIndex++;
            }
        }

        return GetPartition(PartitionIdFromInt(index));
    }

    internal InMemoryPartition GetPartition(string partitionId)
    {
        if (TryGetPartition(partitionId, out var partition))
        {
            return partition;
        }

        throw new InvalidOperationException($"Partition '{partitionId}' not found in event hub '{Name}' in namespace {Namespace.Name}.");
    }

    internal bool TryGetPartition(string partitionId, [NotNullWhen(true)] out InMemoryPartition? partition)
    {
        return _partitions.TryGetValue(partitionId, out partition);
    }

    internal InMemoryPartition GetPartitionByKey(string partitionKey)
    {
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(partitionKey));

        var hashCode = BinaryPrimitives.ReadInt32BigEndian(hashBytes.AsSpan(0, 4));

        hashCode -= int.MinValue;

        var partitionId = hashCode % Properties.PartitionIds.Length;

        return GetPartition(PartitionIdFromInt(partitionId));
    }

    private static string PartitionIdFromInt(int partitionId) => partitionId.ToString(CultureInfo.InvariantCulture);


}
