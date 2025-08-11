using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus.Internals;
using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus;

public sealed class InMemoryServiceBusProcessor : ServiceBusProcessor
{
    private readonly TimeSpan _defaultMaxWaitTime;
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly SemaphoreSlim _stateSemaphore = new(1, 1);
    private readonly InMemoryServiceBusReceiver _receiver;
    
    private volatile bool _isClosed;
    private volatile bool _isProcessing;
    private CancellationTokenSource? _processingCts;
    private Task? _processingTask;

    public InMemoryServiceBusProcessor(InMemoryServiceBusClient client, string queueName)
        : this(client, queueName, new ServiceBusProcessorOptions()) {}
    public InMemoryServiceBusProcessor(InMemoryServiceBusClient client, string queueName, ServiceBusProcessorOptions options)
        : base(client, queueName, options)
    {
        _receiver = new InMemoryServiceBusReceiver(client, queueName, new ServiceBusReceiverOptions 
        { 
            ReceiveMode = options.ReceiveMode,
            PrefetchCount = options.PrefetchCount,
            Identifier = options.Identifier
        });
        _defaultMaxWaitTime = client.DefaultMaxWaitTime;
        _concurrencySemaphore = new SemaphoreSlim(MaxConcurrentCalls, MaxConcurrentCalls);
        Provider = client.Provider;
    }
    public InMemoryServiceBusProcessor(InMemoryServiceBusClient client, string topicName, string subscriptionName, ServiceBusProcessorOptions options)
        : base(client, topicName, subscriptionName, options) 
    {
        _receiver = new InMemoryServiceBusReceiver(client, topicName, subscriptionName, new ServiceBusReceiverOptions 
        { 
            ReceiveMode = options.ReceiveMode,
            PrefetchCount = options.PrefetchCount,
            Identifier = options.Identifier
        });
        _defaultMaxWaitTime = client.DefaultMaxWaitTime;
        _concurrencySemaphore = new SemaphoreSlim(MaxConcurrentCalls, MaxConcurrentCalls);
        Provider = client.Provider;
    }
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
    

    public InMemoryServiceBusProvider Provider { get; }

    public override bool IsClosed => _isClosed;
    public override bool IsProcessing => _isProcessing;

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
        await base.CloseAsync(cancellationToken);
    }

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
            
            await base.StartProcessingAsync(cancellationToken);

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

            // Cancel processing
            _processingCts?.Cancel();

   
            if (_processingTask != null)
            {
                try
                {
                    await _processingTask;
                }
                catch (OperationCanceledException)
                {
                }
                finally
                {
                    _processingTask?.Dispose();
                    _processingTask = null;
                }
            }

            _isProcessing = false;
            
            await base.StopProcessingAsync(cancellationToken);
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
                    var messages = await ReceiveMessagesCoreAsync(
                        MaxConcurrentCalls,
                        _defaultMaxWaitTime,
                        cancellationToken);

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
                try
                {
                    await Task.WhenAll(activeTasks);
                }
                catch
                {
                    // Ignore exceptions during cleanup
                }
            }
        }
    }
    
    private async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesCoreAsync(
        int maxMessages, 
        TimeSpan maxWaitTime, 
        CancellationToken cancellationToken)
    {
        return await _receiver.ReceiveMessagesAsync(maxMessages, maxWaitTime, cancellationToken);
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
                try
                {
                    await _receiver.CompleteMessageAsync(message, cancellationToken);
                }
                catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
                {
                    // Message was already settled by user - this is fine for auto-complete
                }
            }
            
        }
        catch (Exception ex)
        {
            try
            {
                await _receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
            }
            catch (ServiceBusException abandonEx) when (abandonEx.Reason == ServiceBusFailureReason.MessageLockLost)
            {
                // Message was already settled - ignore
            }
            catch
            {
                // Ignore other abandon errors
            }


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
}
