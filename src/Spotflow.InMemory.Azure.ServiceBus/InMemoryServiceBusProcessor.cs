using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus.Internals;
using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus;

public class InMemoryServiceBusProcessor : ServiceBusProcessor
{
    private readonly TimeSpan _defaultMaxWaitTime;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly SemaphoreSlim _stateSemaphore = new(1, 1);
    private readonly InMemoryServiceBusReceiver? _receiver;
    private readonly bool _autoCompleteMessages;
    private readonly string _entityPath;

    private volatile bool _isClosed;
    private volatile bool _isProcessing;
    private CancellationTokenSource? _processingCts;
    private Task? _processingTask;

    private readonly string _identifier;
    private readonly string _fullyQualifiedNamespace;
    private readonly ServiceBusReceiveMode _receiveMode;
    private readonly int _prefetchCount;
    private readonly int _maxConcurrentCalls;
    private readonly TimeSpan _maxAutoLockRenewalDuration;

    private readonly InMemoryServiceBusSessionProcessor? _sessionProcessor;
    private readonly bool _isSessionProcessor;

    #region Constructors
    internal InMemoryServiceBusProcessor(InMemoryServiceBusClient client, string queueName)
        : this(client, queueName, new ServiceBusProcessorOptions()) { }

    internal InMemoryServiceBusProcessor(InMemoryServiceBusClient client, string queueName, ServiceBusProcessorOptions options)
        : this(client, queueName, false, options,
            (receiverOptions, c)
                => new InMemoryServiceBusReceiver(c, queueName, receiverOptions))
    { }

    internal InMemoryServiceBusProcessor(InMemoryServiceBusClient client, string topicName, string subscriptionName)
        : this(client, topicName, subscriptionName, new ServiceBusProcessorOptions()) { }

    internal InMemoryServiceBusProcessor(InMemoryServiceBusClient client, string topicName, string subscriptionName, ServiceBusProcessorOptions options)
        : this(client, FormatEntityPath(topicName, subscriptionName), false, options,
            (receiverOptions, c)
                => new InMemoryServiceBusReceiver(c, topicName, subscriptionName, receiverOptions))
    { }

    internal InMemoryServiceBusProcessor(
        InMemoryServiceBusClient client,
        string entityPath,
        bool isSessionEntity,
        ServiceBusProcessorOptions options,
        Func<ServiceBusReceiverOptions, InMemoryServiceBusClient, InMemoryServiceBusReceiver?>? receiverFactory,
        string[]? sessionIds = null,
        int maxConcurrentSessions = 0,
        int maxConcurrentCallsPerSession = 0,
        InMemoryServiceBusSessionProcessor? sessionProcessor = null)
    {
        _fullyQualifiedNamespace = client.FullyQualifiedNamespace;
        _identifier = string.IsNullOrEmpty(options.Identifier) ? ServiceBusClientUtils.GenerateIdentifier(entityPath) : options.Identifier;
        _entityPath = entityPath;
        _defaultMaxWaitTime = client.DefaultMaxWaitTime;
        _autoCompleteMessages = options.AutoCompleteMessages;
        _receiveMode = options.ReceiveMode;
        _prefetchCount = options.PrefetchCount;
        _maxConcurrentCalls = options.MaxConcurrentCalls;
        _maxAutoLockRenewalDuration = options.MaxAutoLockRenewalDuration;
        Provider = client.Provider;
        _isSessionProcessor = isSessionEntity;

        if (isSessionEntity)
        {
            _sessionProcessor = sessionProcessor;
            _receiver = null;
            _maxConcurrentCalls = (sessionIds is { Length: > 0 }
                ? Math.Min(sessionIds.Length, maxConcurrentSessions)
                : maxConcurrentSessions) * maxConcurrentCallsPerSession;
        }
        else
        {
            ArgumentNullException.ThrowIfNull(receiverFactory);
            _sessionProcessor = null;
            _receiver = receiverFactory(CreateReceiverOptions(options, _identifier), client);
        }
        _concurrencySemaphore = new SemaphoreSlim(_maxConcurrentCalls, _maxConcurrentCalls);

    }

    private static ServiceBusReceiverOptions CreateReceiverOptions(ServiceBusProcessorOptions options, string identifier)
     => new()
     {
         ReceiveMode = options.ReceiveMode,
         PrefetchCount = options.PrefetchCount,
         Identifier = $"{identifier}-receiver"
     };


    private static string FormatEntityPath(string topicName, string subscriptionName)
        => InMemoryServiceBusSubscription.FormatEntityPath(topicName, subscriptionName);

    public static InMemoryServiceBusProcessor FromQueue(InMemoryServiceBusQueue queue, ServiceBusProcessorOptions? options = null)
    {
        var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);
        return new InMemoryServiceBusProcessor(client, queue.QueueName, options ?? new ServiceBusProcessorOptions());
    }

    public static InMemoryServiceBusProcessor FromSubscription(InMemoryServiceBusSubscription subscription, ServiceBusProcessorOptions? options = null)
    {
        var client = InMemoryServiceBusClient.FromNamespace(subscription.Topic.Namespace);
        return new InMemoryServiceBusProcessor(client, subscription.TopicName, subscription.SubscriptionName, options ?? new ServiceBusProcessorOptions());
    }
    #endregion

    #region Properties
    public override bool AutoCompleteMessages => _autoCompleteMessages;
    public override string FullyQualifiedNamespace => _fullyQualifiedNamespace;
    public override string EntityPath => _entityPath;
    public override string Identifier => _identifier;
    public override ServiceBusReceiveMode ReceiveMode => _receiveMode;
    public override int PrefetchCount => _prefetchCount;
    public override int MaxConcurrentCalls => _maxConcurrentCalls;
    public override TimeSpan MaxAutoLockRenewalDuration => _maxAutoLockRenewalDuration;
    public InMemoryServiceBusProvider Provider { get; }
    public override bool IsClosed => _isClosed;
    public override bool IsProcessing => _isProcessing;

    #endregion

    #region Close
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
        if (!_isSessionProcessor)
        {
            ArgumentNullException.ThrowIfNull(_receiver);
            await _receiver.DisposeAsync();
        }

    }
    #endregion

    #region Start/Stop Processing
    public override async Task StartProcessingAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        await _stateSemaphore.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_isClosed, nameof(InMemoryServiceBusProcessor));

            if (_isProcessing)
            {
                throw new InvalidOperationException("The processor is already processing messages.");
            }

            _isProcessing = true;
            _processingCts = new CancellationTokenSource();
            _processingTask = _isSessionProcessor
                ? Task.Run(() => _sessionProcessor?.ProcessSessionsInBackgroundAsync(_processingCts.Token), cancellationToken)
                : Task.Run(() => ProcessMessagesInBackground(_processingCts.Token), cancellationToken);
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

    /// <summary>
    /// StopProcessingUnsafeAsync is used to avoid deadlock between <see cref="CloseAsync"/> and <see cref="StopProcessingAsync"/>
    /// </summary>
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

    private async Task ProcessMessagesInBackground(CancellationToken cancellationToken)
    {
        var activeTasks = new List<Task>();
        ArgumentNullException.ThrowIfNull(_receiver);
        try
        {
            while (!cancellationToken.IsCancellationRequested && !_isClosed)
            {
                try
                {
                    var messages = await _receiver.ReceiveMessagesAsync(MaxConcurrentCalls, _defaultMaxWaitTime, cancellationToken);

                    if (messages.Count == 0)
                    {
                        continue;
                    }

                    foreach (var message in messages)
                    {
                        await _concurrencySemaphore.WaitAsync(cancellationToken);
                        Task messageTask;
                        try
                        {
                            messageTask = Task.Run(() => ProcessSingleMessageAsync(message, cancellationToken), cancellationToken);
                        }
                        catch
                        {
                            _concurrencySemaphore.Release();
                            throw;
                        }

                        activeTasks.Add(messageTask);

                        if (activeTasks.Count > MaxConcurrentCalls)
                        {
                            activeTasks.RemoveAll(t => t.IsCompleted);
                        }
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



    private async Task ProcessSingleMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_receiver);
        try
        {
            try
            {
                var processMessageEventArgs = new ProcessMessageEventArgs(
                    message,
                    _receiver,
                    Identifier,
                    cancellationToken);

                await OnProcessMessageAsync(processMessageEventArgs);
                if (AutoCompleteMessages)
                {
                    await _receiver.CompleteMessageAsync(message, cancellationToken);
                }

            }
            catch (Exception ex)
            {
                await _receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                await HandleErrorAsync(ex, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Suppress OperationCanceledException to prevent it from interrupting processor shutdown
            return;
        }
        finally
        {
            _concurrencySemaphore.Release();
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
            // Suppress OperationCanceledException to prevent it from interrupting processor shutdown
            return;
        }
    }
    #endregion

}
