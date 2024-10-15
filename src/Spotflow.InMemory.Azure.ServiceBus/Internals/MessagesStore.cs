using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Internals;

internal class MessagesStore(TimeProvider timeProvider, TimeSpan lockTime)
{
    private record EnqueuedServiceBusMessage(ServiceBusMessage Message, long SequenceNumber, DateTimeOffset EnqueuedTime);
    private record LockedServiceBusMessage(ServiceBusMessage Message, long SequenceNumber, DateTimeOffset LockedUntil);

    private readonly object _syncObj = new();

    private readonly Queue<EnqueuedServiceBusMessage> _enqueuedMessages = new();
    private readonly Queue<EnqueuedServiceBusMessage> _reenqueuedMessages = new();
    private readonly Dictionary<Guid, LockedServiceBusMessage> _lockedMessages = [];

    private readonly ManualResetEventSlim _newMessageAdded = new(false);

    public int ActiveMessageCount
    {
        get
        {
            lock (_syncObj)
            {
                ReleaseExpiredMessagesUnsafe();
                return _enqueuedMessages.Count + _reenqueuedMessages.Count;
            }
        }
    }

    public int MessageCount
    {
        get
        {
            lock (_syncObj)
            {
                ReleaseExpiredMessagesUnsafe();
                return _enqueuedMessages.Count + _reenqueuedMessages.Count + _lockedMessages.Count;
            }
        }
    }

    public void AddMessage(ServiceBusMessage message, long sequenceNumber)
    {
        var enqueuedMessage = new EnqueuedServiceBusMessage(message, sequenceNumber, timeProvider.GetUtcNow());

        lock (_syncObj)
        {
            _enqueuedMessages.Enqueue(enqueuedMessage);
            _newMessageAdded.Set();
        }
    }

    public void AddMessages(IReadOnlyList<ServiceBusMessage> messages, long firstSequenceNumber)
    {
        var now = timeProvider.GetUtcNow();

        lock (_syncObj)
        {
            var currentSequenceNumber = firstSequenceNumber;

            foreach (var message in messages)
            {
                var enqueuedMessage = new EnqueuedServiceBusMessage(message, currentSequenceNumber++, now);
                _enqueuedMessages.Enqueue(enqueuedMessage);
            }

            _newMessageAdded.Set();
        }
    }

    public async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveAsync(int maxMessages, TimeSpan maxWaitTime, ServiceBusReceiveMode receiveMode, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxMessages, 1);

        await Task.Yield();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        linkedCts.CancelAfter(maxWaitTime);

        lock (_syncObj)
        {
            ReleaseExpiredMessagesUnsafe();
        }

        var result = new List<ServiceBusReceivedMessage>(maxMessages);

        while (true)
        {

            // Throw if user cancelled the operation and no messages are available.
            // Make sure to check the original caller CT.

            cancellationToken.ThrowIfCancellationRequested();

            lock (_syncObj)
            {
                while (result.Count < maxMessages && _reenqueuedMessages.TryDequeue(out var expiredMessage))
                {
                    var receivedMessage = FinishReceiveMessageUnsafe(expiredMessage, receiveMode);
                    result.Add(receivedMessage);
                }

                while (result.Count < maxMessages && _enqueuedMessages.TryDequeue(out var enqueuedMessage))
                {
                    var receivedMessage = FinishReceiveMessageUnsafe(enqueuedMessage, receiveMode);
                    result.Add(receivedMessage);
                }

                if (result.Count > 0)
                {
                    return result;
                }

                _newMessageAdded.Reset();
            }

            try
            {
                _newMessageAdded.Wait(linkedCts.Token); // Make sure to check the linked token
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) // Requested is cancelled by the caller
            {
                throw;
            }
            catch (OperationCanceledException) // Max wait time is reached
            {
                return result;
            }

        }

    }

    public bool CompleteMessage(ServiceBusReceivedMessage message)
    {
        var lockToken = GetLockToken(message);

        lock (_syncObj)
        {
            ReleaseExpiredMessagesUnsafe();

            if (!_lockedMessages.Remove(lockToken, out var lockedMessage))
            {
                return false;
            }

            if (IsMessageLockExpired(lockedMessage))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
    }

    public void AbandonMessage(ServiceBusReceivedMessage message)
    {
        var lockToken = GetLockToken(message);

        lock (_syncObj)
        {
            ReleaseExpiredMessagesUnsafe();

            TryUnlockMessageAndReenqueueUnsafe(lockToken);
        }
    }

    public bool RenewMessageLock(ServiceBusReceivedMessage message)
    {
        var lockToken = GetLockToken(message);

        lock (_syncObj)
        {
            ReleaseExpiredMessagesUnsafe();

            if (!_lockedMessages.TryGetValue(lockToken, out var lockedMessage))
            {
                return false;
            }

            if (IsMessageLockExpired(lockedMessage))
            {
                return false;
            }

            _lockedMessages[lockToken] = lockedMessage with { LockedUntil = timeProvider.GetUtcNow().Add(lockTime) };

            return true;

        }
    }

    private ServiceBusReceivedMessage FinishReceiveMessageUnsafe(EnqueuedServiceBusMessage enqueuedMessage, ServiceBusReceiveMode receiveMode)
    {
        Guid lockToken;
        DateTimeOffset lockedUntil;

        var message = enqueuedMessage.Message;
        var sequenceNumber = enqueuedMessage.SequenceNumber;

        if (receiveMode is ServiceBusReceiveMode.PeekLock)
        {
            lockToken = Guid.NewGuid();
            lockedUntil = timeProvider.GetUtcNow().Add(lockTime);

            var lockedMessage = new LockedServiceBusMessage(message, sequenceNumber, lockedUntil);

            if (!_lockedMessages.TryAdd(lockToken, lockedMessage))
            {
                throw new InvalidOperationException("Failed to lock message. The lock token is already in use.");
            }
        }
        else if (receiveMode is ServiceBusReceiveMode.ReceiveAndDelete)
        {
            lockToken = default;
            lockedUntil = default;
        }
        else
        {
            throw new InvalidOperationException($"Unsupported receive mode: {receiveMode}.");
        }

        return ServiceBusModelFactory.ServiceBusReceivedMessage(
              body: message.Body,
              messageId: message.MessageId,
              sessionId: message.SessionId,
              replyToSessionId: message.ReplyToSessionId,
              replyTo: message.ReplyTo,
              timeToLive: message.TimeToLive,
              correlationId: message.CorrelationId,
              contentType: message.ContentType,
              enqueuedTime: timeProvider.GetUtcNow(),
              properties: message.ApplicationProperties,
              subject: message.Subject,
              lockTokenGuid: lockToken,
              lockedUntil: lockedUntil,
              sequenceNumber: sequenceNumber,
              partitionKey: message.PartitionKey
              );
    }

    private void ReleaseExpiredMessagesUnsafe()
    {
        foreach (var (lockToken, lockedMessage) in _lockedMessages)
        {
            if (IsMessageLockExpired(lockedMessage))
            {
                TryUnlockMessageAndReenqueueUnsafe(lockToken);
            }
        }
    }

    private bool IsMessageLockExpired(LockedServiceBusMessage lockedMessage)
    {
        return lockedMessage.LockedUntil < timeProvider.GetUtcNow();
    }

    private void TryUnlockMessageAndReenqueueUnsafe(Guid lockToken)
    {
        if (_lockedMessages.Remove(lockToken, out var lockedMessage))
        {
            var requenquedMessage = new EnqueuedServiceBusMessage(lockedMessage.Message, lockedMessage.SequenceNumber, timeProvider.GetUtcNow());

            _reenqueuedMessages.Enqueue(requenquedMessage);
        }
    }


    private static Guid GetLockToken(ServiceBusReceivedMessage message) => Guid.Parse(message.LockToken);
}
