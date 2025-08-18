using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus;

public class InMemoryServiceBusProcessor : ServiceBusProcessor
{
    private readonly TimeSpan _defaultMaxWaitTime;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly SemaphoreSlim _stateSemaphore = new(1, 1);
    private readonly InMemoryServiceBusReceiver _receiver;
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

    #region Constructors
    public InMemoryServiceBusProcessor(InMemoryServiceBusClient client, string queueName)
        : this(client, queueName, new ServiceBusProcessorOptions()) { }

    public InMemoryServiceBusProcessor(InMemoryServiceBusClient client, string queueName, ServiceBusProcessorOptions options)
        : this(client, queueName, options, new InMemoryServiceBusReceiver(client, queueName, CreateReceiverOptions(options))) { }
    
    public InMemoryServiceBusProcessor(InMemoryServiceBusClient client, string topicName, string subscriptionName)
        : this(client, topicName, subscriptionName, new ServiceBusProcessorOptions()) { }

    public InMemoryServiceBusProcessor(InMemoryServiceBusClient client, string topicName, string subscriptionName, ServiceBusProcessorOptions options)
        : this(client, FormatEntityPath(topicName, subscriptionName), options, 
            new InMemoryServiceBusReceiver(client, topicName, subscriptionName, CreateReceiverOptions(options))) { }

    private InMemoryServiceBusProcessor(
        InMemoryServiceBusClient client, 
        string entityPath, 
        ServiceBusProcessorOptions options,
        InMemoryServiceBusReceiver receiver)
    {
        _fullyQualifiedNamespace = client.FullyQualifiedNamespace;
        _identifier = options.Identifier ?? GenerateIdentifier(entityPath);
        _entityPath = entityPath;
        _defaultMaxWaitTime = client.DefaultMaxWaitTime;
        _autoCompleteMessages = options.AutoCompleteMessages;
        _receiveMode = options.ReceiveMode;
        _prefetchCount = options.PrefetchCount;
        _maxConcurrentCalls = options.MaxConcurrentCalls;
        _maxAutoLockRenewalDuration = options.MaxAutoLockRenewalDuration;
        _receiver = receiver;
        Provider = client.Provider;
        _concurrencySemaphore = new SemaphoreSlim(_maxConcurrentCalls, _maxConcurrentCalls);
    }

    /// <summary>
    /// Helper method to generate identifier based on
    /// https://github.com/Azure/azure-sdk-for-net/blob/5cb4a6d5dad39a25a1854fe4c4be6bffab745785/sdk/servicebus/Azure.Messaging.ServiceBus/src/Diagnostics/DiagnosticUtilities.cs#L10
    /// Todo: move this to a utility helper class
    /// </summary>
    /// <param name="entityPath"></param>
    /// <returns></returns>
    private static string GenerateIdentifier(string entityPath) => $"{entityPath}-{Guid.NewGuid()}"; 

    private static ServiceBusReceiverOptions CreateReceiverOptions(ServiceBusProcessorOptions options)
     => new()
        {
            ReceiveMode = options.ReceiveMode,
            PrefetchCount = options.PrefetchCount,
            Identifier = $"{options.Identifier}-receiver"
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

        _isClosed = true;

        if (_isProcessing)
        {
            await StopProcessingAsync(cancellationToken);
        }
        _concurrencySemaphore.Dispose();
        _stateSemaphore.Dispose();
        _processingCts?.Dispose();
        await _receiver.DisposeAsync();
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
            _processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _processingTask = ProcessMessagesInBackground(_processingCts.Token);
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
                    _processingTask?.Dispose();
                    _processingTask = null;
                }
            }

            _isProcessing = false;
            
        }
        finally
        {
            _stateSemaphore.Release();
        }
    }

    private async Task ProcessMessagesInBackground(CancellationToken cancellationToken)
    {
        var activeTasks = new List<Task>();

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

                        var messageTask = ProcessSingleMessageAsync(message, cancellationToken);
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
        finally
        {
            _concurrencySemaphore.Release();
        }
    }
    
    private async Task HandleErrorAsync(Exception exception, CancellationToken cancellationToken)
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
    #endregion
    
}
