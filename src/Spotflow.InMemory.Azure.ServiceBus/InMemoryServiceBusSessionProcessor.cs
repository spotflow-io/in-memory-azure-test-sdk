using System.Collections.Concurrent;

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
    private readonly bool _isQueue;
    private readonly string? _queueName;
    private readonly string? _topicName;
    private readonly string? _subscriptionName;

    public InMemoryServiceBusProvider Provider { get; }

    protected override ServiceBusProcessor InnerProcessor { get; }

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
        var processorOptions = options.ToProcessorOptions();
        InnerProcessor = new InMemoryServiceBusProcessor(
            client,
            entityPath,
            true,
            processorOptions,
            null,
            _sessionIds,
            options.MaxConcurrentSessions,
            options.MaxConcurrentCallsPerSession,
            this
        );

    }

    private static string FormatEntityPath(string topicName, string subscriptionName)
        => InMemoryServiceBusSubscription.FormatEntityPath(topicName, subscriptionName);
    #endregion

    public static InMemoryServiceBusSessionProcessor FromQueue(InMemoryServiceBusQueue queue, ServiceBusSessionProcessorOptions? options = null)
    {
        var sessionOptions = options ?? new ServiceBusSessionProcessorOptions();
        var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);
        return new InMemoryServiceBusSessionProcessor(client, queue.QueueName, sessionOptions);
    }

    public static InMemoryServiceBusSessionProcessor FromSubscription(InMemoryServiceBusSubscription subscription, ServiceBusSessionProcessorOptions? options = null)
    {
        var sessionOptions = options ?? new ServiceBusSessionProcessorOptions();
        var client = InMemoryServiceBusClient.FromNamespace(subscription.Topic.Namespace);
        return new InMemoryServiceBusSessionProcessor(client, subscription.TopicName, subscription.SubscriptionName, sessionOptions);
    }
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
    internal async Task ProcessSessionsInBackground(CancellationToken cancellationToken)
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

                    // accept new sessions if we have capacity
                    if (activeSessions.Count < MaxConcurrentSessions)
                    {
                        await _sessionConcurrencySemaphore.WaitAsync(cancellationToken);
                        try
                        {
                            var sessionReceiver = await TryAcceptNextSessionAsync(cancellationToken);
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
                                activeSessions.TryAdd(sessionReceiver.SessionId, sessionTask);
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
                    else
                    {
                        // give slack when at max capacity
                        await Task.Delay(100, cancellationToken);
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
        var messageCallbacksSemaphore = new SemaphoreSlim(MaxConcurrentCallsPerSession, MaxConcurrentCallsPerSession);
        var messageProcessingTasks = new List<Task>();

        try
        {
            var initArgs = new ProcessSessionEventArgs(sessionReceiver, Identifier, cancellationToken);

            await OnSessionInitializingAsync(initArgs);

            var sessionIdleTimeout = SessionIdleTimeout ?? _defaultMaxWaitTime;
            var lastActivity = DateTimeOffset.UtcNow;

            while (!cancellationToken.IsCancellationRequested && !InnerProcessor.IsClosed && !sessionReceiver.IsClosed)
            {
                try
                {
                    if (DateTimeOffset.UtcNow - lastActivity > sessionIdleTimeout)
                    {
                        break;
                    }

                    var messages = await sessionReceiver.ReceiveMessagesAsync(
                        MaxConcurrentCallsPerSession, SessionIdleTimeout ?? _defaultMaxWaitTime, cancellationToken);

                    if (messages.Count == 0)
                    {
                        continue;
                    }
                    lastActivity = DateTimeOffset.UtcNow;
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
                var closeArgs = new ProcessSessionEventArgs(sessionReceiver, Identifier, cancellationToken);
                await OnSessionClosingAsync(closeArgs);
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex, cancellationToken);
            }
            await sessionReceiver.DisposeAsync();
            messageCallbacksSemaphore.Dispose();
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

    private async Task<InMemoryServiceBusSessionReceiver?> TryAcceptNextSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_sessionIds.Length > 0)
            {
                foreach (var sessionId in _sessionIds)
                {
                    try
                    {
                        // Casting is safe here as AcceptSessionAsync returns InMemoryServiceBusSessionReceiver
                        return _isQueue
                            ? (InMemoryServiceBusSessionReceiver) await _client.AcceptSessionAsync(
                                _queueName!,
                                sessionId,
                                new ServiceBusSessionReceiverOptions(),
                                cancellationToken)
                            : (InMemoryServiceBusSessionReceiver) await _client.AcceptSessionAsync(
                                _topicName!,
                                _subscriptionName!,
                                sessionId,
                                new ServiceBusSessionReceiverOptions(),
                                cancellationToken);

                    }
                    catch (ServiceBusException)
                    {
                        // session locked or does not exist yet, try another one
                        continue;
                    }
                }
                // none of the specified sessions were available
                return null;
            }

            return _isQueue
                ? (InMemoryServiceBusSessionReceiver) await _client.AcceptNextSessionAsync(
                    _queueName!,
                    new ServiceBusSessionReceiverOptions(),
                    cancellationToken)
                : (InMemoryServiceBusSessionReceiver) await _client.AcceptNextSessionAsync(
                    _topicName!,
                    _subscriptionName!,
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
}

internal static class InMemoryServiceBusSessionOptionsExtensions
{
    public static ServiceBusProcessorOptions ToProcessorOptions(this ServiceBusSessionProcessorOptions options)
        => new()
        {
            ReceiveMode = options.ReceiveMode,
            PrefetchCount = options.PrefetchCount,
            AutoCompleteMessages = options.AutoCompleteMessages,
            MaxAutoLockRenewalDuration = options.MaxAutoLockRenewalDuration,
            Identifier = options.Identifier
        };
}
