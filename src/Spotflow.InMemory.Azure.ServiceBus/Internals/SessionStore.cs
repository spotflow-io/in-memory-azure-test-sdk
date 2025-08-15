using System.Diagnostics.CodeAnalysis;

using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Internals;

internal class SessionStore(string fullyQualifiedNamespace, string entityPath, string sessionId, TimeProvider timeProvider, TimeSpan lockTime)
{
    private readonly object _syncObj = new();

    private readonly MessagesStore _messageStore = new(timeProvider, lockTime);

    private Guid? _sessionLockToken;
    private DateTimeOffset? _sessionLockedUntil;
    private BinaryData? _sessionState;

    public string FullyQualifiedNamespace { get; } = fullyQualifiedNamespace;
    public string EntityPath { get; } = entityPath;
    public string SessionId { get; } = sessionId;
    public long ActiveMessageCount => _messageStore.ActiveMessageCount;
    public long MessageCount => _messageStore.MessageCount;

    public bool IsLocked
    {
        get { lock (_syncObj) { return _sessionLockToken.HasValue; } }
    }

    public DateTimeOffset? SessionLockedUntil
    {
        get { lock (_syncObj) { return _sessionLockedUntil; } }
    }

    public bool TryLock(bool allowEmpty, [NotNullWhen(true)] out LockedSession? acquiredSession)
    {
        lock (_syncObj)
        {
            if (_sessionLockToken is not null)
            {
                acquiredSession = null;
                return false;
            }

            if (!allowEmpty && _messageStore.ActiveMessageCount is 0)
            {
                // If there are no messages, we don't lock the session.
                acquiredSession = null;
                return false;
            }

            _sessionLockToken = Guid.NewGuid();
            _sessionLockedUntil = timeProvider.GetUtcNow().Add(lockTime);
            acquiredSession = new LockedSession(this, _sessionLockToken.Value);
            return true;

        }
    }

    public void Release(LockedSession session)
    {
        lock (_syncObj)
        {
            if (_sessionLockToken == session.SessionLockToken)
            {
                _sessionLockToken = null;
                _sessionLockedUntil = null;
            }
        }
    }

    public async Task<SessionResult<IReadOnlyList<ServiceBusReceivedMessage>>> ReceiveAsync(LockedSession session, int maxMessages, TimeSpan maxWaitTime, ServiceBusReceiveMode receiveMode, CancellationToken cancellationToken)
    {
        lock (_syncObj)
        {
            if (!CheckSessionLockUnsafe(session))
            {
                return new(ServiceBusFailureReason.SessionLockLost);
            }
        }

        var messages = await _messageStore.ReceiveAsync(maxMessages, maxWaitTime, receiveMode, cancellationToken);

        return new(messages);
    }

    public SessionResult RenewSessionLock(LockedSession session)
    {
        lock (_syncObj)
        {
            if (!CheckSessionLockUnsafe(session))
            {
                return new(ServiceBusFailureReason.SessionLockLost);
            }

            _sessionLockedUntil = timeProvider.GetUtcNow().Add(lockTime);
            return SessionResult.Successful;
        }
    }

    public SessionResult<BinaryData> GetSessionState(LockedSession session)
    {
        lock (_syncObj)
        {

            if (!CheckSessionLockUnsafe(session))
            {
                return new(ServiceBusFailureReason.SessionLockLost);
            }

            return new(_sessionState ?? new BinaryData(string.Empty));
        }
    }

    public SessionResult SetSessionState(LockedSession session, BinaryData state)
    {
        lock (_syncObj)
        {
            if (!CheckSessionLockUnsafe(session))
            {
                return new(ServiceBusFailureReason.SessionLockLost);
            }

            _sessionState = state;
        }

        return SessionResult.Successful;
    }

    public void AddMessage(ServiceBusMessage message, long sequenceNumber)
    {
        AssertSession(message);
        _messageStore.AddMessage(message, sequenceNumber);
    }

    public void AddMessages(IReadOnlyList<ServiceBusMessage> messages, long firstSequenceNumber)
    {
        AssertSession(messages);
        _messageStore.AddMessages(messages, firstSequenceNumber);
    }

    public SessionResult CompleteMessage(LockedSession session, ServiceBusReceivedMessage message)
    {
        AssertSession(message);
        AssertSession(session);

        lock (_syncObj)
        {
            if (!CheckSessionLockUnsafe(session))
            {
                return new(ServiceBusFailureReason.SessionLockLost);
            }
        }

        if (!_messageStore.CompleteMessage(message))
        {
            return new(ServiceBusFailureReason.MessageLockLost);
        }

        return SessionResult.Successful;
    }

    public SessionResult AbandonMessage(LockedSession session, ServiceBusReceivedMessage message)
    {
        AssertSession(message);

        lock (_syncObj)
        {
            if (!CheckSessionLockUnsafe(session))
            {
                return new(ServiceBusFailureReason.SessionLockLost);
            }
        }

        _messageStore.AbandonMessage(message);

        return SessionResult.Successful;
    }

    public SessionResult RenewMessageLock(LockedSession session, ServiceBusReceivedMessage message)
    {
        AssertSession(message);
        AssertSession(session);

        lock (_syncObj)
        {
            if (!CheckSessionLockUnsafe(session))
            {
                return new(ServiceBusFailureReason.SessionLockLost);
            }
        }

        if (!_messageStore.RenewMessageLock(message))
        {
            return new(ServiceBusFailureReason.MessageLockLost);
        }

        return SessionResult.Successful;
    }


    private bool CheckSessionLockUnsafe(LockedSession session)
    {

        if (_sessionLockToken != session.SessionLockToken)
        {
            return false;
        }

        if (_sessionLockedUntil is null || _sessionLockedUntil < timeProvider.GetUtcNow())
        {
            return false;
        }

        return true;
    }

    private void AssertSession(LockedSession session) => AssertSession(session.SessionId);
    private void AssertSession(ServiceBusMessage message) => AssertSession(message.SessionId);
    private void AssertSession(IEnumerable<ServiceBusMessage> messages)
    {
        foreach (var message in messages)
        {
            AssertSession(message);
        }
    }
    private void AssertSession(ServiceBusReceivedMessage message) => AssertSession(message.SessionId);

    private void AssertSession(string? incomingSessionId)
    {
        if (string.IsNullOrWhiteSpace(incomingSessionId))
        {
            throw new InvalidOperationException("Session id not set.");
        }

        if (SessionId != incomingSessionId)
        {
            throw new InvalidOperationException($"Message (sid = {incomingSessionId}) does not belong to this session (sid = {SessionId}).");
        }
    }


}
