using System.Diagnostics.CodeAnalysis;

using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus.Internals;

namespace Spotflow.InMemory.Azure.ServiceBus.Resources;

public class InMemoryServiceBusSubscription : InMemoryServiceBusEntity, IConsumableEntity
{
    public InMemoryServiceBusSubscription(string subscriptionName, InMemoryServiceBusSubscriptionOptions options, InMemoryServiceBusTopic parentTopic) : base(parentTopic.Namespace)
    {
        SubscriptionName = subscriptionName;
        Topic = parentTopic;
        EnableSessions = options.EnableSessions;
        LockTime = options.LockTime ?? DefaultLockTime;
        EntityPath = FormatEntityPath(parentTopic.TopicName, subscriptionName);

        Engine = EnableSessions switch
        {
            true => new SessionEngine(this),
            false => new SessionlessEngine(this)
        };
    }

    public string SubscriptionName { get; }
    public string TopicName => Topic.TopicName;
    public InMemoryServiceBusTopic Topic { get; }

    public override string EntityPath { get; }

    public long ActiveMessageCount => Engine.ActiveMessageCount;
    public long MessageCount => Engine.MessageCount;

    public bool EnableSessions { get; }

    public TimeSpan LockTime { get; }

    internal IMessagingEngine Engine { get; }

    IMessagingEngine IConsumableEntity.Engine => Engine;

    public static string FormatEntityPath(string topicName, string subscriptionName) => $"{topicName}/subscriptions/{subscriptionName}";

    internal override bool TryAddMessage(ServiceBusMessage message, [NotNullWhen(false)] out AddMessageError? error)
    {
        return Engine.TryAddMessage(message, out error);
    }

    internal override bool TryAddMessages(IReadOnlyList<ServiceBusMessage> messages, [NotNullWhen(false)] out AddMessageError? error)
    {
        return Engine.TryAddMessages(messages, out error);
    }
}


