using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Spotflow.InMemory.Azure.ServiceBus.Internals;

namespace Spotflow.InMemory.Azure.ServiceBus.Resources;

public class InMemoryServiceBusNamespace(string namespaceName, InMemoryServiceBusProvider provider)
{
    private readonly ConcurrentDictionary<string, InMemoryServiceBusEntity> _entities = new();

    internal ILoggerFactory LoggerFactory { get; } = NullLoggerFactory.Instance;

    public string FullyQualifiedNamespace { get; } = $"{namespaceName}.{provider.HostnameSuffix.TrimStart('.')}";
    public string Name => namespaceName;
    public InMemoryServiceBusProvider Provider { get; } = provider;

    public TimeProvider TimeProvider => Provider.TimeProvider;

    public string CreateConnectionString() => ServiceBusConnectionStringUtils.CreateConnectionString(this);

    public InMemoryServiceBusEntity? FindEntity(string entityName) => _entities.TryGetValue(entityName, out var entity) ? entity : null;

    public InMemoryServiceBusQueue? FindQueue(string queueName) => FindEntity(queueName) as InMemoryServiceBusQueue;

    public InMemoryServiceBusTopic? FindTopic(string topicName) => FindEntity(topicName) as InMemoryServiceBusTopic;

    public InMemoryServiceBusQueue AddQueue(string queueName, InMemoryServiceBusQueueOptions? options = null)
    {
        var queue = new InMemoryServiceBusQueue(queueName, options ?? new(), this);

        if (!_entities.TryAdd(queueName, queue))
        {
            throw new InvalidOperationException($"The namespace '{FullyQualifiedNamespace}' already contains entity '{queueName}'.");
        }

        return queue;
    }

    public InMemoryServiceBusTopic AddTopic(string topicName)
    {
        var topic = new InMemoryServiceBusTopic(topicName, this);

        if (!_entities.TryAdd(topicName, topic))
        {
            throw new InvalidOperationException($"The namespace '{FullyQualifiedNamespace}' already contains entity '{topicName}'.");
        }

        return topic;
    }



}
