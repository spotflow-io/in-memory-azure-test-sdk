using System.Collections.Concurrent;

using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus.Hooks;
using Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;
using Spotflow.InMemory.Azure.ServiceBus.Internals;
using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus;
public class InMemoryServiceBusSender : ServiceBusSender
{
    private readonly ConcurrentDictionary<ServiceBusMessageBatch, List<ServiceBusMessage>> _batches = new(ReferenceEqualityComparer.Instance);

    private volatile bool _isClosed = false;

    private readonly ServiceBusProducerHookScope _scope;

    #region Constructors

    public InMemoryServiceBusSender(InMemoryServiceBusClient client, string queueOrTopicName) : this(client, queueOrTopicName, new()) { }

    public InMemoryServiceBusSender(InMemoryServiceBusClient client, string queueOrTopicName, ServiceBusSenderOptions options)
    {
        Identifier = options?.Identifier ?? Guid.NewGuid().ToString();
        FullyQualifiedNamespace = client.FullyQualifiedNamespace;
        EntityPath = queueOrTopicName;
        Provider = client.Provider;
        _scope = new(Provider.GetNamespaceNameFromHostname(client.FullyQualifiedNamespace), queueOrTopicName);
    }

    public static InMemoryServiceBusSender FromQueue(InMemoryServiceBusQueue queue, ServiceBusClientOptions? options = null)
    {
        var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace, options);
        return new(client, queue.QueueName);
    }

    public static InMemoryServiceBusSender FromTopic(InMemoryServiceBusTopic topic, ServiceBusClientOptions? options = null)
    {
        var client = InMemoryServiceBusClient.FromNamespace(topic.Namespace, options);
        return new(client, topic.TopicName);
    }

    #endregion

    public InMemoryServiceBusProvider Provider { get; }

    #region Properties

    public override string FullyQualifiedNamespace { get; }
    public override string EntityPath { get; }
    public override bool IsClosed => _isClosed;
    public override string Identifier { get; }

    #endregion

    #region Dispose & Close

    public override async Task CloseAsync(CancellationToken cancellationToken = default) => await DisposeAsync();

    public override async ValueTask DisposeAsync()
    {
        await Task.Yield();
        _isClosed = true;
    }

    #endregion

    #region Send

    public override async Task SendMessageAsync(ServiceBusMessage message, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var beforeContext = new SendMessageBeforeHookContext(_scope, Provider, cancellationToken)
        {
            Message = message
        };

        await ExecuteBeforeHooksAsync(beforeContext);

        var entity = ServiceBusClientUtils.GetEntity(FullyQualifiedNamespace, EntityPath, Provider);

        if (!entity.TryAddMessage(message, out var error))
        {
            throw error.GetClientException();
        }

        var afterContext = new SendMessageAfterHookContext(beforeContext)
        {
            Message = message
        };

        await ExecuteAfterHooksAsync(afterContext);
    }

    public override async Task SendMessagesAsync(ServiceBusMessageBatch messageBatch, CancellationToken cancellationToken = default)
    {
        if (!_batches.TryRemove(messageBatch, out var messages))
        {
            var e = $"Batch can be sent only once and must be created from the same client instance. Current batch was already sent or originates from different client instance.";
            throw ServiceBusExceptionFactory.FeatureNotSupported(e);
        }

        await SendBatchCoreAsync(messages, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    }

    public override async Task SendMessagesAsync(IEnumerable<ServiceBusMessage> messages, CancellationToken cancellationToken = default)
    {
        await SendBatchCoreAsync(messages.ToList(), cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    }

    private async Task SendBatchCoreAsync(IReadOnlyList<ServiceBusMessage> messages, CancellationToken cancellationToken = default)
    {
        var beforeContext = new SendBatchBeforeHookContext(_scope, Provider, cancellationToken)
        {
            Messages = messages
        };

        await ExecuteBeforeHooksAsync(beforeContext);

        var entity = ServiceBusClientUtils.GetEntity(FullyQualifiedNamespace, EntityPath, Provider);

        if (!entity.TryAddMessages(messages, out var error))
        {
            throw error.GetClientException();
        }

        var afterContext = new SendBatchAfterHookContext(beforeContext)
        {
            Messages = messages
        };

        await ExecuteAfterHooksAsync(afterContext);
    }

    #endregion

    #region CreateMessageBatchAsync

    public override async ValueTask<ServiceBusMessageBatch> CreateMessageBatchAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var messages = new List<ServiceBusMessage>();
        var batch = ServiceBusModelFactory.ServiceBusMessageBatch(-1, messages);

        _batches[batch] = messages;

        return batch;
    }

    public override ValueTask<ServiceBusMessageBatch> CreateMessageBatchAsync(CreateMessageBatchOptions options, CancellationToken cancellationToken = default)
    {
        return CreateMessageBatchAsync(cancellationToken);
    }

    #endregion

    private Task ExecuteBeforeHooksAsync<TContext>(TContext context) where TContext : ProducerBeforeHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

    private Task ExecuteAfterHooksAsync<TContext>(TContext context) where TContext : ProducerAfterHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }



    #region Unsupported

    public override Task<long> ScheduleMessageAsync(ServiceBusMessage message, DateTimeOffset scheduledEnqueueTime, CancellationToken cancellationToken = default)
    {
        throw ServiceBusExceptionFactory.MethodNotSupported();
    }

    public override Task<IReadOnlyList<long>> ScheduleMessagesAsync(IEnumerable<ServiceBusMessage> messages, DateTimeOffset scheduledEnqueueTime, CancellationToken cancellationToken = default)
    {
        throw ServiceBusExceptionFactory.MethodNotSupported();
    }

    public override Task CancelScheduledMessageAsync(long sequenceNumber, CancellationToken cancellationToken = default)
    {
        throw ServiceBusExceptionFactory.MethodNotSupported();
    }

    public override Task CancelScheduledMessagesAsync(IEnumerable<long> sequenceNumbers, CancellationToken cancellationToken = default)
    {
        throw ServiceBusExceptionFactory.MethodNotSupported();
    }
    #endregion

}
