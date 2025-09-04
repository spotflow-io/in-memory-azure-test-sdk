using Azure.Messaging.ServiceBus;
using Spotflow.InMemory.Azure.ServiceBus.Internals;
using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus;

public class InMemoryServiceBusSessionProcessor : ServiceBusSessionProcessor
{
   
    private readonly int _maxConcurrentCallsPerSession;
    private readonly int _maxConcurrentSessions;
    private readonly TimeSpan? _sessionIdleTimeout;
    private readonly TimeSpan _defaultMaxWaitTime;
    private readonly TimeSpan _tryTimeout;
    private readonly SessionEngine _sessionEngine;
    private readonly SemaphoreSlim _sessionConcurrencySemaphore;
    private readonly string[] _sessionIds;
    public InMemoryServiceBusProvider Provider { get; }
    
    protected override ServiceBusProcessor InnerProcessor { get; }

    #region Constructors
    internal InMemoryServiceBusSessionProcessor(InMemoryServiceBusClient client, string queueName, ServiceBusSessionProcessorOptions? options)
        : this(client, queueName, options ?? new ServiceBusSessionProcessorOptions(), () =>
        {
            var queue = ServiceBusClientUtils.GetQueue(client.FullyQualifiedNamespace, queueName, client.Provider);
            return queue.Engine as SessionEngine ?? throw ServiceBusExceptionFactory.SessionsNotEnabled(client.FullyQualifiedNamespace, queue.EntityPath);
        })
    { }
    
    internal InMemoryServiceBusSessionProcessor(
        InMemoryServiceBusClient client,
        string topicName,
        string subscriptionName,
        ServiceBusSessionProcessorOptions? options)
        : this(client, FormatEntityPath(topicName, subscriptionName), options ?? new ServiceBusSessionProcessorOptions(), () =>
        {
            var subscription = ServiceBusClientUtils.GetSubscription(client.FullyQualifiedNamespace, topicName, subscriptionName, client.Provider);
            return subscription.Engine as SessionEngine ?? throw ServiceBusExceptionFactory.SessionsNotEnabled(client.FullyQualifiedNamespace, subscription.EntityPath);
        })
    { }

    private InMemoryServiceBusSessionProcessor(
        InMemoryServiceBusClient client,
        string entityPath,
        ServiceBusSessionProcessorOptions options,
        Func<SessionEngine> sessionEngineFactory)
    {
        _defaultMaxWaitTime = client.DefaultMaxWaitTime;
        _maxConcurrentCallsPerSession = options.MaxConcurrentCallsPerSession;
        _maxConcurrentSessions = options.MaxConcurrentSessions;
        _sessionIdleTimeout = options.SessionIdleTimeout;
        _tryTimeout = client.TryTimeout;
        _sessionConcurrencySemaphore = new SemaphoreSlim(options.MaxConcurrentSessions, options.MaxConcurrentSessions);
        Provider = client.Provider;
       
        _sessionEngine = sessionEngineFactory();
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
    
    private async Task ProcessSingleSessionAsync(InMemoryServiceBusSessionReceiver sessionReceiver, CancellationToken cancellationToken)
    {
        var messageCallbacksSemaphore = new SemaphoreSlim(MaxConcurrentCallsPerSession, MaxConcurrentCallsPerSession);
        var messageProcessingTasks = new List<Task>();
        
        try
        {
            var initArgs = new ProcessSessionEventArgs(sessionReceiver, cancellationToken);
            
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
                            var messageTask = ProcessingSingleSessionMessageAsync(message, sessionReceiver, messageCallbacksSemaphore, cancellationToken);
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
                await Task.WhenAll(messageProcessingTasks);
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex, cancellationToken);
            }
            try
            {
                var closeArgs = new ProcessSessionEventArgs(sessionReceiver, cancellationToken);
                await OnSessionClosingAsync(closeArgs);
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex, cancellationToken);
            }
            await sessionReceiver.DisposeAsync();
            messageCallbacksSemaphore.Dispose();
            _sessionConcurrencySemaphore.Release();
        }
    }
    
    private async Task ProcessingSingleSessionMessageAsync(
        ServiceBusReceivedMessage message,
        InMemoryServiceBusSessionReceiver sessionReceiver,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                var processArgs = new ProcessSessionMessageEventArgs(message, sessionReceiver, cancellationToken);
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
            return;
        }
        finally
        {
            semaphore.Release();
        }
    }
    
    internal async Task ProcessSessionsInBackground(CancellationToken cancellationToken)
    {
        var sessionTasks = new List<Task>();
        try
        {
            while (!cancellationToken.IsCancellationRequested && !InnerProcessor.IsClosed)
            {
                try
                {
                    sessionTasks.RemoveAll(t => t.IsCompleted);
                    // accept new sessions if we have capacity
                    if (sessionTasks.Count < MaxConcurrentSessions)
                    {
                        await _sessionConcurrencySemaphore.WaitAsync(cancellationToken);
                        try
                        {
                            var sessionReceiver = await TryAcceptNextSessionAsync(cancellationToken);
                            if (sessionReceiver != null)
                            {
                                
                                var processingTask = Task.Run(() => ProcessSingleSessionAsync(sessionReceiver, cancellationToken), cancellationToken);
                                sessionTasks.Add(processingTask);
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

                    //await Task.Delay(100, cancellationToken);
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
            if (sessionTasks.Count > 0)
            {
                await Task.WhenAll(sessionTasks);
            }
        }
    }

    private async Task<InMemoryServiceBusSessionReceiver?> TryAcceptNextSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            LockedSession? session;
            if (_sessionIds.Length > 0)
            {
                foreach (var sessionId in _sessionIds)
                {
                    if (_sessionEngine.TryAcquireSession(sessionId, out session))
                    {
                        return new InMemoryServiceBusSessionReceiver(session, new ServiceBusSessionReceiverOptions(), _defaultMaxWaitTime, Provider);
                    }
                }

                return null;
            }
            session = await _sessionEngine.TryAcquireNextAvailableSessionAsync(_tryTimeout, cancellationToken);
            return session == null 
                ? null 
                : new InMemoryServiceBusSessionReceiver(session, new ServiceBusSessionReceiverOptions(), _defaultMaxWaitTime, Provider);
        }
        catch (Exception)
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
