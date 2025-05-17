using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Logging;

using Spotflow.InMemory.Azure.ServiceBus.Internals;

namespace Spotflow.InMemory.Azure.ServiceBus.Resources;

public class InMemoryServiceBusTopic(string topicName, InMemoryServiceBusNamespace parentNamespace)
    : InMemoryServiceBusEntity(parentNamespace)
{
    private readonly ILogger<InMemoryServiceBusTopic> _logger = parentNamespace.LoggerFactory.CreateLogger<InMemoryServiceBusTopic>();
    private readonly ConcurrentDictionary<string, InMemoryServiceBusSubscription> _subscriptions = new();

    public string TopicName { get; } = topicName;

    public override string EntityPath => TopicName;


    internal override bool TryAddMessage(ServiceBusMessage message, [NotNullWhen(false)] out AddMessageError? error)
    {
        foreach (var subscription in _subscriptions.Values)
        {
            if (!subscription.TryAddMessage(message, out var subscriptionError))
            {
                _logger.LogWarning(
                   "Message could not be added to subscription {fqn}/{path}: {subscriptionError}",
                   subscription.FullyQualifiedNamespace,
                   subscription.EntityPath,
                   subscriptionError
                   );
            }
        }

        error = null;
        return true;
    }

    internal override bool TryAddMessages(IReadOnlyList<ServiceBusMessage> messages, [NotNullWhen(false)] out AddMessageError? error)
    {
        foreach (var (_, subscription) in _subscriptions)
        {
            if (!subscription.TryAddMessages(messages, out var subscriptionError))
            {
                _logger.LogWarning(
                    "Message could not be added to subscription {fqn}/{path}: {subscriptionError}",
                    subscription.FullyQualifiedNamespace,
                    subscription.EntityPath,
                    subscriptionError
                    );
            }
        }

        error = null;
        return true;
    }

    public InMemoryServiceBusSubscription? FindSubscription(string subscriptionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

        if (!_subscriptions.TryGetValue(subscriptionName, out var subscription))
        {
            return null;
        }

        return subscription;
    }

    public InMemoryServiceBusSubscription GetSubscription(string subscriptionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

        var sub = FindSubscription(subscriptionName);

        if (sub is null)
        {
            throw new InvalidOperationException($"Subscription '{subscriptionName}' not found in topic '{Namespace.Name}/{TopicName}'");
        }

        return sub;
    }

    public InMemoryServiceBusSubscription AddSubscription(string subscriptionName, InMemoryServiceBusSubscriptionOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);

        var subscription = new InMemoryServiceBusSubscription(subscriptionName, options ?? new(), this);

        if (!_subscriptions.TryAdd(subscriptionName, subscription))
        {
            throw new InvalidOperationException($"Subscription '{subscriptionName}' already exists.");
        }

        return subscription;
    }
}
