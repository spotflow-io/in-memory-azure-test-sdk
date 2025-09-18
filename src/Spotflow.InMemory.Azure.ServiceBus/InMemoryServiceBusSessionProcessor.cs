using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus;

public class InMemoryServiceBusSessionProcessor : ServiceBusSessionProcessor
{

    private readonly int _maxConcurrentCallsPerSession;
    private readonly int _maxConcurrentSessions;
    private readonly TimeSpan? _sessionIdleTimeout;
    private readonly TimeSpan _defaultMaxWaitTime;
    private readonly SemaphoreSlim _sessionConcurrencySemaphore;
    private readonly string[] _sessionIds;
    private readonly InMemoryServiceBusClient _client;

    private readonly string? _queueName;
    private readonly string? _topicName;
    private readonly string? _subscriptionName;

    [MemberNotNullWhen(true, nameof(_queueName))]
    [MemberNotNullWhen(false, nameof(_topicName), nameof(_subscriptionName))]
    private bool _isQueue { get; }
    public InMemoryServiceBusProvider Provider { get; }

    protected override ServiceBusProcessor InnerProcessor { get; }

    private static readonly FieldInfo? _sessionInitializingAsyncField;
    private static readonly FieldInfo? _sessionClosingAsyncField;
    static InMemoryServiceBusSessionProcessor()
    {
        var processorType = typeof(ServiceBusProcessor);
        _sessionInitializingAsyncField = processorType.GetField("_sessionInitializingAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        _sessionClosingAsyncField = processorType.GetField("_sessionClosingAsync", BindingFlags.NonPublic | BindingFlags.Instance);
    }

    #region Constructors
    internal InMemoryServiceBusSessionProcessor(
        InMemoryServiceBusClient client,
        string queueName,
        ServiceBusSessionProcessorOptions? options)
        : this(client, queueName, null, null, options ?? new ServiceBusSessionProcessorOptions())
    { }

    internal InMemoryServiceBusSessionProcessor(
        InMemoryServiceBusClient client,
        string topicName,
        string subscriptionName,
        ServiceBusSessionProcessorOptions? options)
        : this(client, null, topicName, subscriptionName, options ?? new ServiceBusSessionProcessorOptions())
    { }

    private InMemoryServiceBusSessionProcessor(
        InMemoryServiceBusClient client,
        string? queueName,
        string? topicName,
        string? subscriptionName,
        ServiceBusSessionProcessorOptions options)
    {

        _client = client;
        _queueName = queueName;
        _topicName = topicName;
        _subscriptionName = subscriptionName;
        _isQueue = _queueName != null;
        string? entityPath;
        if (_isQueue)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
            entityPath = queueName;
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
            ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionName);
            entityPath = FormatEntityPath(topicName, subscriptionName);
        }

        _defaultMaxWaitTime = client.DefaultMaxWaitTime;
        _maxConcurrentCallsPerSession = options.MaxConcurrentCallsPerSession;
        _maxConcurrentSessions = options.MaxConcurrentSessions;
        _sessionIdleTimeout = options.SessionIdleTimeout;
        _sessionConcurrencySemaphore = new SemaphoreSlim(options.MaxConcurrentSessions, options.MaxConcurrentSessions);
        Provider = client.Provider;

        _sessionIds = options.SessionIds.ToArray();
        var processorOptions = ConvertToProcessorOptions(options);
        InnerProcessor = new InMemoryServiceBusProcessor(
            client: client,
            entityPath: entityPath,
            isSessionEntity: true,
            options: processorOptions,
            receiverFactory: null,
            sessionIds: _sessionIds,
            maxConcurrentSessions: options.MaxConcurrentSessions,
            maxConcurrentCallsPerSession: options.MaxConcurrentCallsPerSession,
            sessionProcessor: this
        );

    }

    private static string FormatEntityPath(string topicName, string subscriptionName)
        => InMemoryServiceBusSubscription.FormatEntityPath(topicName, subscriptionName);
    #endregion

    #region Properties

    public override string FullyQualifiedNamespace => InnerProcessor.FullyQualifiedNamespace;
    public override string EntityPath => InnerProcessor.EntityPath;
    public override bool IsClosed => InnerProcessor.IsClosed;
    public override bool AutoCompleteMessages => InnerProcessor.AutoCompleteMessages;
    public override bool IsProcessing => InnerProcessor.IsProcessing;
    public override string Identifier => InnerProcessor.Identifier;
    public override TimeSpan MaxAutoLockRenewalDuration => InnerProcessor.MaxAutoLockRenewalDuration;
    public override int PrefetchCount => InnerProcessor.PrefetchCount;
    public override ServiceBusReceiveMode ReceiveMode => InnerProcessor.ReceiveMode;
    public override int MaxConcurrentCallsPerSession => _maxConcurrentCallsPerSession;
    public override int MaxConcurrentSessions => _maxConcurrentSessions;
    public override TimeSpan? SessionIdleTimeout => _sessionIdleTimeout;

    #endregion

    #region Close and Dispose
    public override async Task CloseAsync(CancellationToken cancellationToken = new())
    {

        await InnerProcessor.CloseAsync(cancellationToken);
        _sessionConcurrencySemaphore.Dispose();

    }
    #endregion

    #region Start/Stop Processing
    public override async Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        await InnerProcessor.StartProcessingAsync(cancellationToken);
    }

    public override async Task StopProcessingAsync(CancellationToken cancellationToken = default)
    {
        await InnerProcessor.StopProcessingAsync(cancellationToken);
    }
    internal async Task ProcessSessionsInBackgroundAsync(CancellationToken cancellationToken)
    {
        var activeSessions = new ConcurrentDictionary<string, Task>();
        try
        {
            while (!cancellationToken.IsCancellationRequested && !InnerProcessor.IsClosed)
            {
                try
                {
                    var completedSessions = activeSessions
                        .Where(s => s.Value.IsCompleted)
                        .Select(x => x.Key)
                        .ToList();

                    foreach (var sessionId in completedSessions)
                    {
                        activeSessions.TryRemove(sessionId, out _);
                    }
                    var waitTimeout = TimeSpan.FromMilliseconds(100);
                    var acquired = await _sessionConcurrencySemaphore.WaitAsync(waitTimeout, cancellationToken);
                    if (acquired)
                    {
                        try
                        {
                            var sessionReceiver = await TryAcceptNextSessionAsync(activeSessions, cancellationToken);
                            if (sessionReceiver != null)
                            {
                                var sessionTask = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await ProcessSingleSessionAsync(sessionReceiver, cancellationToken);
                                    }
                                    finally
                                    {
                                        _sessionConcurrencySemaphore.Release();
                                    }
                                }, cancellationToken);

                                if (!activeSessions.TryAdd(sessionReceiver.SessionId, sessionTask))
                                {
                                    await sessionReceiver.DisposeAsync();
                                    throw new InvalidOperationException($"Failed to add session {sessionReceiver.SessionId} to active sessions.");
                                }
                            }
                            else
                            {
                                _sessionConcurrencySemaphore.Release();
                            }
                        }
                        catch (Exception)
                        {
                            _sessionConcurrencySemaphore.Release();
                            throw;
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception e)
                {
                    await HandleErrorAsync(e, cancellationToken);
                }
            }
        }
        finally
        {
            if (!activeSessions.IsEmpty)
            {
                await Task.WhenAll(activeSessions.Values);
            }
        }
    }
    private async Task ProcessSingleSessionAsync(InMemoryServiceBusSessionReceiver sessionReceiver, CancellationToken cancellationToken)
    {
        using var messageCallbacksSemaphore = new SemaphoreSlim(MaxConcurrentCallsPerSession, MaxConcurrentCallsPerSession);
        await using (sessionReceiver)
        {
            var messageProcessingTasks = new List<Task>();

            try
            {
                // using reflection to check if _sessionInitializingAsync is instantiated
                var sessionInitHandler = _sessionInitializingAsyncField?.GetValue(InnerProcessor);
                if (sessionInitHandler != null)
                {
                    var initArgs = new ProcessSessionEventArgs(sessionReceiver, Identifier, cancellationToken);
                    await OnSessionInitializingAsync(initArgs);
                }
                var sessionIdleTimeout = SessionIdleTimeout ?? _defaultMaxWaitTime;
                var lastActivity = Provider.TimeProvider.GetTimestamp();

                while (!cancellationToken.IsCancellationRequested && !InnerProcessor.IsClosed && !sessionReceiver.IsClosed)
                {
                    try
                    {
                        var elapsed = Provider.TimeProvider.GetElapsedTime(lastActivity);
                        if (elapsed > sessionIdleTimeout)
                        {
                            break;
                        }

                        var messages = await sessionReceiver.ReceiveMessagesAsync(
                            MaxConcurrentCallsPerSession, SessionIdleTimeout ?? _defaultMaxWaitTime, cancellationToken);

                        if (messages.Count == 0)
                        {
                            continue;
                        }
                        lastActivity = Provider.TimeProvider.GetTimestamp();
                        foreach (var message in messages)
                        {
                            await messageCallbacksSemaphore.WaitAsync(cancellationToken);

                            try
                            {
                                var messageTask = StartMessageProcessingTask(message, sessionReceiver, messageCallbacksSemaphore, cancellationToken);
                                messageProcessingTasks.Add(messageTask);
                            }
                            catch (Exception)
                            {
                                messageCallbacksSemaphore.Release();
                                throw;
                            }
                            messageProcessingTasks.RemoveAll(t => t.IsCompleted);

                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        await HandleErrorAsync(ex, cancellationToken);
                    }
                }
            }
            finally
            {
                try
                {
                    if (messageProcessingTasks.Count > 0)
                    {
                        await Task.WhenAll(messageProcessingTasks);
                    }
                }
                catch (Exception ex)
                {
                    await HandleErrorAsync(ex, cancellationToken);
                }
                try
                {
                    // using reflection to check if _sessionClosingAsync is instantiated
                    var sessionCloseHandler = _sessionClosingAsyncField?.GetValue(InnerProcessor);
                    if (sessionCloseHandler != null)
                    {
                        var closeArgs = new ProcessSessionEventArgs(sessionReceiver, Identifier, cancellationToken);
                        await OnSessionClosingAsync(closeArgs);
                    }
                }
                catch (Exception ex)
                {
                    await HandleErrorAsync(ex, cancellationToken);
                }
            }
        }

    }

    private Task StartMessageProcessingTask(
        ServiceBusReceivedMessage message,
        InMemoryServiceBusSessionReceiver sessionReceiver,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            try
            {
                await ProcessingSingleSessionMessageAsync(message, sessionReceiver, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        }, cancellationToken);
    }

    private async Task ProcessingSingleSessionMessageAsync(
        ServiceBusReceivedMessage message,
        InMemoryServiceBusSessionReceiver sessionReceiver,
        CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                var processArgs = new ProcessSessionMessageEventArgs(
                    message,
                    sessionReceiver,
                    Identifier,
                    cancellationToken);
                await OnProcessSessionMessageAsync(processArgs);
                if (AutoCompleteMessages)
                {
                    await sessionReceiver.CompleteMessageAsync(message, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                await sessionReceiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                await HandleErrorAsync(ex, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Suppress OperationCanceledException to prevent it from interrupting processor shutdown
        }
    }

    private async Task<InMemoryServiceBusSessionReceiver?> TryAcceptNextSessionAsync(
        ConcurrentDictionary<string, Task> activeSessions,
        CancellationToken cancellationToken)
    {

        if (_sessionIds.Length > 0)
        {
            foreach (var sessionId in _sessionIds)
            {
                // Skip sessions that are already being processed
                if (activeSessions.ContainsKey(sessionId))
                {
                    continue;
                }
                try
                {
                    // Casting is safe here as AcceptSessionAsync returns InMemoryServiceBusSessionReceiver
                    return _isQueue
                        ? (InMemoryServiceBusSessionReceiver) await _client.AcceptSessionAsync(
                            _queueName,
                            sessionId,
                            new ServiceBusSessionReceiverOptions(),
                            cancellationToken)
                        : (InMemoryServiceBusSessionReceiver) await _client.AcceptSessionAsync(
                            _topicName,
                            _subscriptionName,
                            sessionId,
                            new ServiceBusSessionReceiverOptions(),
                            cancellationToken);

                }
                catch (ServiceBusException ex)
                   when (ex.Reason is ServiceBusFailureReason.SessionLockLost or ServiceBusFailureReason.SessionCannotBeLocked)
                {
                    // session temporarily unavailable (lock expired or already locked)
                    // continue to try the next session in our list rather than giving up entirely
                    continue;
                }
            }
            // none of the specified sessions were available
            return null;
        }

        try
        {
            return _isQueue
                ? (InMemoryServiceBusSessionReceiver) await _client.AcceptNextSessionAsync(
                    _queueName,
                    new ServiceBusSessionReceiverOptions(),
                    cancellationToken)
                : (InMemoryServiceBusSessionReceiver) await _client.AcceptNextSessionAsync(
                    _topicName,
                    _subscriptionName,
                    new ServiceBusSessionReceiverOptions(),
                    cancellationToken);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
        {
            return null;
        }

    }

    private async Task HandleErrorAsync(Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            var errorArgs = new ProcessErrorEventArgs(
                exception,
                ServiceBusErrorSource.Receive,
                FullyQualifiedNamespace,
                EntityPath,
                Identifier,
                cancellationToken);
            await OnProcessErrorAsync(errorArgs);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Suppress cancellation exceptions during shutdown
        }
    }
    #endregion

    private static ServiceBusProcessorOptions ConvertToProcessorOptions(ServiceBusSessionProcessorOptions options)
        => new()
        {
            ReceiveMode = options.ReceiveMode,
            PrefetchCount = options.PrefetchCount,
            AutoCompleteMessages = options.AutoCompleteMessages,
            MaxAutoLockRenewalDuration = options.MaxAutoLockRenewalDuration,
            Identifier = options.Identifier
        };
}

