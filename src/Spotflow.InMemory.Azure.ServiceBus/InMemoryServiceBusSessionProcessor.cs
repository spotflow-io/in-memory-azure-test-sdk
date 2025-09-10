using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus.Internals;
using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus;

public class InMemoryServiceBusSessionProcessor : ServiceBusSessionProcessor
{
    private readonly TimeSpan _defaultMaxWaitTime;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly SemaphoreSlim _stateSemaphore = new(1, 1);
    private readonly InMemoryServiceBusClient _client;
    private readonly bool _autoCompleteMessages;
    private readonly string _entityPath;
    private readonly string? _queueName;
    private readonly string? _topicName;
    private readonly string? _subscriptionName;

    private volatile bool _isClosed;
    private volatile bool _isProcessing;
    private CancellationTokenSource? _processingCts;
    private Task? _processingTask;

    private readonly string _identifier;
    private readonly string _fullyQualifiedNamespace;
    private readonly ServiceBusReceiveMode _receiveMode;
    private readonly int _prefetchCount;
    private readonly int _maxConcurrentSessions;
    private readonly TimeSpan _maxAutoLockRenewalDuration;
    private readonly TimeSpan? _sessionIdleTimeout;

    // Private fields for session processor implementation

    #region Constructors
    public InMemoryServiceBusSessionProcessor(InMemoryServiceBusClient client, string queueName)
        : this(client, queueName, new ServiceBusSessionProcessorOptions()) { }

    public InMemoryServiceBusSessionProcessor(InMemoryServiceBusClient client, string queueName, ServiceBusSessionProcessorOptions options)
    {
        _client = client;
        _queueName = queueName;
        _entityPath = queueName;
        
        _fullyQualifiedNamespace = client.FullyQualifiedNamespace;
        _identifier = string.IsNullOrEmpty(options.Identifier) ? ServiceBusClientUtils.GenerateIdentifier(queueName) : options.Identifier;
        _defaultMaxWaitTime = client.DefaultMaxWaitTime;
        _autoCompleteMessages = options.AutoCompleteMessages;
        _receiveMode = options.ReceiveMode;
        _prefetchCount = options.PrefetchCount;
        _maxConcurrentSessions = options.MaxConcurrentSessions;
        _maxAutoLockRenewalDuration = options.MaxAutoLockRenewalDuration;
        _sessionIdleTimeout = options.SessionIdleTimeout;
        Provider = client.Provider;
        _concurrencySemaphore = new SemaphoreSlim(_maxConcurrentSessions, _maxConcurrentSessions);
    }

    public InMemoryServiceBusSessionProcessor(InMemoryServiceBusClient client, string topicName, string subscriptionName)
        : this(client, topicName, subscriptionName, new ServiceBusSessionProcessorOptions()) { }

    public InMemoryServiceBusSessionProcessor(InMemoryServiceBusClient client, string topicName, string subscriptionName, ServiceBusSessionProcessorOptions options)
    {
        _client = client;
        _topicName = topicName;
        _subscriptionName = subscriptionName;
        _entityPath = FormatEntityPath(topicName, subscriptionName);
        
        _fullyQualifiedNamespace = client.FullyQualifiedNamespace;
        _identifier = string.IsNullOrEmpty(options.Identifier) ? ServiceBusClientUtils.GenerateIdentifier(_entityPath) : options.Identifier;
        _defaultMaxWaitTime = client.DefaultMaxWaitTime;
        _autoCompleteMessages = options.AutoCompleteMessages;
        _receiveMode = options.ReceiveMode;
        _prefetchCount = options.PrefetchCount;
        _maxConcurrentSessions = options.MaxConcurrentSessions;
        _maxAutoLockRenewalDuration = options.MaxAutoLockRenewalDuration;
        _sessionIdleTimeout = options.SessionIdleTimeout;
        Provider = client.Provider;
        _concurrencySemaphore = new SemaphoreSlim(_maxConcurrentSessions, _maxConcurrentSessions);
    }

    private static string FormatEntityPath(string topicName, string subscriptionName)
        => InMemoryServiceBusSubscription.FormatEntityPath(topicName, subscriptionName);
    #endregion

    public InMemoryServiceBusProvider Provider { get; init; }

    #region Properties
    public override bool AutoCompleteMessages => _autoCompleteMessages;
    public override string FullyQualifiedNamespace => _fullyQualifiedNamespace;
    public override string EntityPath => _entityPath;
    public override string Identifier => _identifier;
    public override ServiceBusReceiveMode ReceiveMode => _receiveMode;
    public override int PrefetchCount => _prefetchCount;
    public override int MaxConcurrentSessions => _maxConcurrentSessions;
    public override TimeSpan MaxAutoLockRenewalDuration => _maxAutoLockRenewalDuration;
    public override TimeSpan? SessionIdleTimeout => _sessionIdleTimeout;
    public override bool IsClosed => _isClosed;
    public override bool IsProcessing => _isProcessing;
    #endregion

    #region Static Factory Methods
    public static InMemoryServiceBusSessionProcessor FromQueue(InMemoryServiceBusQueue queue, ServiceBusSessionProcessorOptions? options = null)
    {
        var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);
        return new InMemoryServiceBusSessionProcessor(client, queue.QueueName, options ?? new ServiceBusSessionProcessorOptions());
    }

    public static InMemoryServiceBusSessionProcessor FromSubscription(InMemoryServiceBusSubscription subscription, ServiceBusSessionProcessorOptions? options = null)
    {
        var client = InMemoryServiceBusClient.FromNamespace(subscription.Topic.Namespace);
        return new InMemoryServiceBusSessionProcessor(client, subscription.TopicName, subscription.SubscriptionName, options ?? new ServiceBusSessionProcessorOptions());
    }
    #endregion

    #region Lifecycle Methods
    public override async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        await _stateSemaphore.WaitAsync(cancellationToken);
        try
        {
            _isClosed = true;
            if (_isProcessing)
            {
                await StopProcessingUnsafeAsync();
            }
        }
        finally
        {
            _stateSemaphore.Release();
        }
        _concurrencySemaphore.Dispose();
        _stateSemaphore.Dispose();
        _processingCts?.Dispose();
    }

    public new async ValueTask DisposeAsync() => await CloseAsync();

    public override async Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        await _stateSemaphore.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_isClosed, nameof(InMemoryServiceBusSessionProcessor));

            if (_isProcessing)
            {
                throw new InvalidOperationException("The processor is already processing messages.");
            }

            _isProcessing = true;
            _processingCts = new CancellationTokenSource();
            _processingTask = Task.Run(() => ProcessSessionsInBackground(_processingCts.Token), cancellationToken);
        }
        finally
        {
            _stateSemaphore.Release();
        }
    }

    public override async Task StopProcessingAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        await _stateSemaphore.WaitAsync(cancellationToken);
        try
        {
            await StopProcessingUnsafeAsync();
        }
        finally
        {
            _stateSemaphore.Release();
        }
    }

    private async Task StopProcessingUnsafeAsync()
    {
        if (!_isProcessing)
        {
            return;
        }

        _processingCts?.Cancel();

        if (_processingTask != null)
        {
            try
            {
                await _processingTask;
            }
            finally
            {
                _processingTask.Dispose();
                _processingTask = null;
            }
        }

        _isProcessing = false;
    }
    #endregion

    #region Session Processing Logic
    private async Task ProcessSessionsInBackground(CancellationToken cancellationToken)
    {
        var activeTasks = new List<Task>();

        try
        {
            while (!cancellationToken.IsCancellationRequested && !_isClosed)
            {
                try
                {
                    // Try to accept a next available session
                    InMemoryServiceBusSessionReceiver? sessionReceiver = null;
                    try
                    {
                        var receiver = _topicName != null && _subscriptionName != null
                            ? await _client.AcceptNextSessionAsync(_topicName, _subscriptionName, CreateSessionReceiverOptions(), cancellationToken)
                            : await _client.AcceptNextSessionAsync(_queueName!, CreateSessionReceiverOptions(), cancellationToken);
                        sessionReceiver = (InMemoryServiceBusSessionReceiver)receiver;
                    }
                    catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
                    {
                        // No sessions available, continue polling
                        continue;
                    }

                    if (sessionReceiver == null)
                    {
                        continue;
                    }

                    await _concurrencySemaphore.WaitAsync(cancellationToken);
                    Task sessionTask;
                    try
                    {
                        sessionTask = Task.Run(() => ProcessSingleSessionAsync(sessionReceiver, cancellationToken), cancellationToken);
                    }
                    catch
                    {
                        _concurrencySemaphore.Release();
                        await sessionReceiver.DisposeAsync();
                        throw;
                    }

                    activeTasks.Add(sessionTask);

                    // Clean up completed tasks to prevent unlimited growth
                    if (activeTasks.Count > MaxConcurrentSessions)
                    {
                        activeTasks.RemoveAll(t => t.IsCompleted);
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
            if (activeTasks.Count > 0)
            {
                await Task.WhenAll(activeTasks);
            }
        }
    }

    private async Task ProcessSingleSessionAsync(InMemoryServiceBusSessionReceiver sessionReceiver, CancellationToken cancellationToken)
    {
        try
        {
            // Call SessionInitializingAsync
            var sessionEventArgs = new ProcessSessionEventArgs(sessionReceiver, cancellationToken);
            await base.OnSessionInitializingAsync(sessionEventArgs);

            var sessionIdleTimer = DateTime.UtcNow;
            
            while (!cancellationToken.IsCancellationRequested && !_isClosed)
            {
                try
                {
                    var messages = await sessionReceiver.ReceiveMessagesAsync(_maxConcurrentSessions, _defaultMaxWaitTime, cancellationToken);

                    if (messages.Count == 0)
                    {
                        // Check if session has been idle for too long
                        if (_sessionIdleTimeout.HasValue && DateTime.UtcNow - sessionIdleTimer > _sessionIdleTimeout.Value)
                        {
                            break;
                        }
                        continue;
                    }

                    sessionIdleTimer = DateTime.UtcNow; // Reset idle timer when messages are received

                    var messageTasks = new List<Task>();
                    foreach (var message in messages)
                    {
                        var messageTask = ProcessSingleMessageAsync(sessionReceiver, message, cancellationToken);
                        messageTasks.Add(messageTask);
                    }

                    await Task.WhenAll(messageTasks);
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
                // Call SessionClosingAsync
                var sessionEventArgs = new ProcessSessionEventArgs(sessionReceiver, cancellationToken);
                await base.OnSessionClosingAsync(sessionEventArgs);
            }
            finally
            {
                _concurrencySemaphore.Release();
                await sessionReceiver.DisposeAsync();
            }
        }
    }

    private async Task ProcessSingleMessageAsync(InMemoryServiceBusSessionReceiver sessionReceiver, ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                var processSessionMessageEventArgs = new ProcessSessionMessageEventArgs(
                    message,
                    sessionReceiver,
                    Identifier,
                    cancellationToken);

                await base.OnProcessSessionMessageAsync(processSessionMessageEventArgs);
                
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
            return;
        }
    }

    private ServiceBusSessionReceiverOptions CreateSessionReceiverOptions()
    {
        return new ServiceBusSessionReceiverOptions
        {
            ReceiveMode = ReceiveMode,
            PrefetchCount = PrefetchCount,
            Identifier = $"{Identifier}-sessionreceiver"
        };
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

            await base.OnProcessErrorAsync(errorArgs);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Suppress OperationCanceledException to prevent it from interrupting processor shutdown
            return;
        }
    }
    // Override the protected virtual methods to handle events
    #endregion
}
