using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus.Internals;

namespace Spotflow.InMemory.Azure.ServiceBus;

public class InMemoryServiceBusSessionReceiver : ServiceBusSessionReceiver
{
    private readonly LockedSession _session;
    private readonly TimeSpan _defaultMaxWaitTime;
    private readonly string _identifier;
    private readonly int _prefetchCount;
    private readonly SessionStore _store;

    private volatile bool _isClosed;

    internal InMemoryServiceBusSessionReceiver(LockedSession session, ServiceBusSessionReceiverOptions options, TimeSpan defaultMaxWaitTime, InMemoryServiceBusProvider provider)
    {
        _session = session;
        _store = session.Store;
        _defaultMaxWaitTime = defaultMaxWaitTime;
        ReceiveMode = options.ReceiveMode;
        _identifier = options.Identifier ?? Guid.NewGuid().ToString();
        _prefetchCount = options.PrefetchCount;
        Provider = provider;
    }

    public InMemoryServiceBusProvider Provider { get; }

    #region Properties
    public override string FullyQualifiedNamespace => _session.FullyQualifiedNamespace;
    public override string EntityPath => _session.EntityPath;
    public override ServiceBusReceiveMode ReceiveMode { get; }
    public override string Identifier => _identifier;
    public override int PrefetchCount => _prefetchCount;
    public override string SessionId => _session.SessionId;
    public override bool IsClosed => _isClosed;
    public override DateTimeOffset SessionLockedUntil => _store.SessionLockedUntil ?? DateTimeOffset.MinValue;

    #endregion

    #region Close & Dispose

    public override async Task CloseAsync(CancellationToken cancellationToken = default) => await DisposeAsync();

    public override async ValueTask DisposeAsync()
    {
        await Task.Yield();

        _store.Release(_session);

        _isClosed = true;
    }

    #endregion

    #region Receive

    public override async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(int maxMessages, TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var result = await _store.ReceiveAsync(_session, maxMessages, maxWaitTime ?? _defaultMaxWaitTime, ReceiveMode, cancellationToken);

        if (!result.IsSuccessful)
        {
            throw ServiceBusExceptionFactory.SessionReceiveFailed(result.Error.Value, FullyQualifiedNamespace, EntityPath, SessionId);
        }

        return result.Value;
    }

    public override IAsyncEnumerable<ServiceBusReceivedMessage> ReceiveMessagesAsync(CancellationToken cancellationToken = default)
    {
        return ServiceBusClientUtils.ReceiveAsAsyncEnumerable(_session, ReceiveMode, cancellationToken);
    }

    public override async Task<ServiceBusReceivedMessage?> ReceiveMessageAsync(TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return await ServiceBusClientUtils.ReceiveSingleAsync(_session, maxWaitTime ?? _defaultMaxWaitTime, ReceiveMode, cancellationToken);
    }

    #endregion

    #region Abandon, Complete, Renew Message

    public override async Task AbandonMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        if (propertiesToModify is not null)
        {
            throw ServiceBusExceptionFactory.FeatureNotSupported("Properties cannot be modified.");
        }

        var result = _store.AbandonMessage(_session, message);

        if (!result.IsSuccessful)
        {
            throw ServiceBusExceptionFactory.SessionAbandonMessageFailed(result.Error.Value, FullyQualifiedNamespace, EntityPath, SessionId);
        }

    }

    public override async Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var result = _store.CompleteMessage(_session, message);

        if (!result.IsSuccessful)
        {
            throw ServiceBusExceptionFactory.SessionCompleteMessageFailed(result.Error.Value, FullyQualifiedNamespace, EntityPath, SessionId);
        }
    }

    public override async Task RenewMessageLockAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var result = _store.RenewMessageLock(_session, message);

        if (!result.IsSuccessful)
        {
            throw ServiceBusExceptionFactory.SessionRenewMessageFailed(result.Error.Value, FullyQualifiedNamespace, EntityPath, SessionId);
        }
    }

    #endregion

    #region Renew Session

    public override async Task RenewSessionLockAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var result = _store.RenewSessionLock(_session);

        if (!result.IsSuccessful)
        {
            throw ServiceBusExceptionFactory.SessionRenewFailed(result.Error.Value, FullyQualifiedNamespace, EntityPath, SessionId);
        }
    }

    #endregion

    #region Session State

    public override async Task<BinaryData> GetSessionStateAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var result = _store.GetSessionState(_session);

        if (!result.IsSuccessful)
        {
            throw ServiceBusExceptionFactory.SessionStateGetSetFailed(result.Error.Value, FullyQualifiedNamespace, EntityPath, SessionId);
        }

        return result.Value;
    }

    public override async Task SetSessionStateAsync(BinaryData sessionState, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var result = _store.SetSessionState(_session, sessionState);

        if (!result.IsSuccessful)
        {
            throw ServiceBusExceptionFactory.SessionStateGetSetFailed(result.Error.Value, FullyQualifiedNamespace, EntityPath, SessionId);
        }
    }

    #endregion

    #region Unsupported

    public override Task<ServiceBusReceivedMessage> PeekMessageAsync(long? fromSequenceNumber = null, CancellationToken cancellationToken = default) => throw ServiceBusExceptionFactory.MethodNotSupported();

    public override Task<IReadOnlyList<ServiceBusReceivedMessage>> PeekMessagesAsync(int maxMessages, long? fromSequenceNumber = null, CancellationToken cancellationToken = default) => throw ServiceBusExceptionFactory.MethodNotSupported();

    public override Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default) => throw ServiceBusExceptionFactory.MethodNotSupported();

    public override Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object> propertiesToModify, string deadLetterReason, string? deadLetterErrorDescription = null, CancellationToken cancellationToken = default) => throw ServiceBusExceptionFactory.MethodNotSupported();

    public override Task DeadLetterMessageAsync(ServiceBusReceivedMessage message, string deadLetterReason, string? deadLetterErrorDescription = null, CancellationToken cancellationToken = default) => throw ServiceBusExceptionFactory.MethodNotSupported();

    public override Task DeferMessageAsync(ServiceBusReceivedMessage message, IDictionary<string, object>? propertiesToModify = null, CancellationToken cancellationToken = default) => throw ServiceBusExceptionFactory.MethodNotSupported();

    public override Task<ServiceBusReceivedMessage> ReceiveDeferredMessageAsync(long sequenceNumber, CancellationToken cancellationToken = default) => throw ServiceBusExceptionFactory.MethodNotSupported();

    public override Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveDeferredMessagesAsync(IEnumerable<long> sequenceNumbers, CancellationToken cancellationToken = default) => throw ServiceBusExceptionFactory.MethodNotSupported();

    #endregion

}
