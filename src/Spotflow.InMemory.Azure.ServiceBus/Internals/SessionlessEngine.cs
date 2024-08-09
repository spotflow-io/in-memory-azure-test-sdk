using System.Diagnostics.CodeAnalysis;

using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Internals;

internal class SessionlessEngine(IConsumableEntity entity) : IMessagingEngine
{
    private readonly MessagesStore _store = new(entity.TimeProvider, entity.LockTime);
    private readonly MessageSequenceIdGenerator _sequenceIdGenerator = new();


    public long ActiveMessageCount => _store.ActiveMessageCount;
    public long MessageCount => _store.MessageCount;


    public bool TryAddMessage(ServiceBusMessage message, [NotNullWhen(false)] out AddMessageError? error)
    {
        var sequenceNumber = _sequenceIdGenerator.GetNext();
        _store.AddMessage(message, sequenceNumber);
        error = null;
        return true;
    }

    public bool TryAddMessages(IReadOnlyList<ServiceBusMessage> messages, [NotNullWhen(false)] out AddMessageError? error)
    {
        var firstSequenceNumber = _sequenceIdGenerator.GetNext(messages.Count);
        _store.AddMessages(messages, firstSequenceNumber);
        error = null;
        return true;
    }

    public Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveAsync(int maxMessages, TimeSpan maxWaitTime, ServiceBusReceiveMode receiveMode, CancellationToken cancellationToken)
    {
        return _store.ReceiveAsync(maxMessages, maxWaitTime, receiveMode, cancellationToken);
    }

    public bool CompleteMessage(ServiceBusReceivedMessage message) => _store.CompleteMessage(message);

    public void AbandonMessage(ServiceBusReceivedMessage message) => _store.AbandonMessage(message);

    public bool RenewMessageLock(ServiceBusReceivedMessage message) => _store.RenewMessageLock(message);
}
