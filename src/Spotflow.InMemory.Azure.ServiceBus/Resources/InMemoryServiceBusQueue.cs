using System.Diagnostics.CodeAnalysis;

using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus.Internals;

namespace Spotflow.InMemory.Azure.ServiceBus.Resources;

public class InMemoryServiceBusQueue : InMemoryServiceBusEntity, IConsumableEntity
{
    public InMemoryServiceBusQueue(string queueName, InMemoryServiceBusQueueOptions options, InMemoryServiceBusNamespace serviceBusNamespace) : base(serviceBusNamespace)
    {
        EnableSessions = options.EnableSessions;
        LockTime = options.LockTime ?? DefaultLockTime;
        QueueName = queueName;

        Engine = EnableSessions switch
        {
            true => new SessionEngine(this),
            false => new SessionlessEngine(this)
        };
    }

    public string QueueName { get; }
    public bool EnableSessions { get; }
    public TimeSpan LockTime { get; }

    public override string EntityPath => QueueName;
    public long ActiveMessageCount => Engine.ActiveMessageCount;
    public long MessageCount => Engine.MessageCount;

    internal IMessagingEngine Engine { get; }

    IMessagingEngine IConsumableEntity.Engine => Engine;

    internal override bool TryAddMessage(ServiceBusMessage message, [NotNullWhen(false)] out AddMessageError? error)
    {
        return Engine.TryAddMessage(message, out error);
    }

    internal override bool TryAddMessages(IReadOnlyList<ServiceBusMessage> messages, [NotNullWhen(false)] out AddMessageError? error)
    {
        return Engine.TryAddMessages(messages, out error);
    }
}
