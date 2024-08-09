using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Internals;

internal class SessionEngine(IConsumableEntity entity) : IMessagingEngine
{
    private readonly ConcurrentDictionary<string, SessionStore> _sessions = new();

    public long ActiveMessageCount => _sessions.Values.Sum(s => s.ActiveMessageCount);
    public long MessageCount => _sessions.Values.Sum(s => s.MessageCount);

    private readonly MessageSequenceIdGenerator _sequenceIdGenerator = new();

    public bool TryAddMessage(ServiceBusMessage message, [NotNullWhen(false)] out AddMessageError? error)
    {
        if (!HasSessionId(message))
        {
            error = new AddMessageError.SessionIdNotSetOnMessage(entity.FullyQualifiedNamespace, entity.EntityPath);
            return false;
        }

        var sessionId = message.SessionId;

        var sessionStore = _sessions.GetOrAdd(sessionId, (s) => new SessionStore(entity.FullyQualifiedNamespace, entity.EntityPath, sessionId, entity.TimeProvider, entity.LockTime));

        var sequenceNumber = _sequenceIdGenerator.GetNext();

        sessionStore.AddMessage(message, sequenceNumber);

        error = null;
        return true;
    }

    public bool TryAddMessages(IReadOnlyList<ServiceBusMessage> messages, [NotNullWhen(false)] out AddMessageError? error)
    {
        foreach (var message in messages)
        {
            if (!HasSessionId(message))
            {
                error = new AddMessageError.SessionIdNotSetOnMessage(entity.FullyQualifiedNamespace, entity.EntityPath);
                return false;
            }
        }

        var firstMessageSequenceNumber = _sequenceIdGenerator.GetNext(messages.Count);

        var currentMessageSequenceNumber = firstMessageSequenceNumber;

        foreach (var sessionGroup in messages.GroupBy(m => m.SessionId))
        {
            var sessionId = sessionGroup.Key;
            var sessionMessages = sessionGroup.ToList();

            var session = _sessions.GetOrAdd(sessionId, (s) => new SessionStore(entity.FullyQualifiedNamespace, entity.EntityPath, sessionId, entity.TimeProvider, entity.LockTime));

            session.AddMessages(sessionMessages, currentMessageSequenceNumber);
            currentMessageSequenceNumber += sessionMessages.Count;
        }

        error = null;
        return true;
    }

    public async Task<LockedSession?> TryAcquireNextAvailableSessionAsync(TimeSpan maxDelay, CancellationToken cancellationToken)
    {
        var start = entity.TimeProvider.GetTimestamp();

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var (_, sessionStore) in _sessions)
            {
                if (sessionStore.TryLockIfNotEmpty(out var acquiredSession))
                {
                    return acquiredSession;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken: cancellationToken);

        } while (entity.TimeProvider.GetElapsedTime(start) < maxDelay);

        return null;
    }

    public bool TryAcquireSession(string sessionId, [NotNullWhen(true)] out LockedSession? session)
    {
        if (_sessions.TryGetValue(sessionId, out var sessionStore))
        {
            if (sessionStore.TryLockIfNotEmpty(out var acquiredSession))
            {
                session = acquiredSession;
                return true;
            }
        }

        session = null;
        return false;
    }

    private static bool HasSessionId(ServiceBusMessage message) => !string.IsNullOrWhiteSpace(message.SessionId);
}
