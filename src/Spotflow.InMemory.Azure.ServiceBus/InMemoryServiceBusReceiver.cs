using System.Runtime.CompilerServices;

using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus.Hooks;
using Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;
using Spotflow.InMemory.Azure.ServiceBus.Internals;
using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus;

public class InMemoryServiceBusReceiver : ServiceBusReceiver
{
    private readonly string _identifier;
    private readonly TimeSpan _defaultMaxWaitTime;
    private readonly int _prefetchCount;
    private readonly Func<SessionlessEngine> _getStore;
    private readonly ServiceBusConsumerHookScope _scope;

    private volatile bool _isClosed = false;

    #region Constructors

    public InMemoryServiceBusReceiver(InMemoryServiceBusClient client, string queueName)
        : this(client, queueName, new ServiceBusReceiverOptions()) { }
    public InMemoryServiceBusReceiver(InMemoryServiceBusClient client, string queueName, ServiceBusReceiverOptions options)
        : this(client, queueName, options, () => GetStoreForQueue(client, queueName), isTopicSubscription: false) { }

    public InMemoryServiceBusReceiver(InMemoryServiceBusClient client, string topicName, string subscriptionName)
        : this(client, FormatEntityPath(topicName, subscriptionName), subscriptionName, new ServiceBusReceiverOptions()) { }

    public InMemoryServiceBusReceiver(InMemoryServiceBusClient client, string topicName, string subscriptionName, ServiceBusReceiverOptions options)
        : this(client, FormatEntityPath(topicName, subscriptionName), options, () => GetStoreForSubscription(client, topicName, subscriptionName), isTopicSubscription: true) { }

    private static string FormatEntityPath(string topicName, string subscriptionName)
        => InMemoryServiceBusSubscription.FormatEntityPath(topicName, subscriptionName);

    private static SessionlessEngine GetStoreForQueue(InMemoryServiceBusClient client, string queueName)
    {
        IConsumableEntity queue = ServiceBusClientUtils.GetQueue(client.FullyQualifiedNamespace, queueName, client.Provider);

        var store = queue.Engine as SessionlessEngine;

        return store ?? throw ServiceBusExceptionFactory.SessionsEnabled(queue.FullyQualifiedNamespace, queue.EntityPath);
    }

    private static SessionlessEngine GetStoreForSubscription(InMemoryServiceBusClient client, string topicName, string subscriptionName)
    {
        var subscription = ServiceBusClientUtils.GetSubscription(client.FullyQualifiedNamespace, topicName, subscriptionName, client.Provider);

        var store = subscription.Engine as SessionlessEngine;

        return store ?? throw ServiceBusExceptionFactory.SessionsEnabled(subscription.FullyQualifiedNamespace, subscription.EntityPath);

    }

    private InMemoryServiceBusReceiver(
        InMemoryServiceBusClient client,
        string entityPath,
        ServiceBusReceiverOptions options,
        Func<SessionlessEngine> getStore,
        bool isTopicSubscription)
    {
        FullyQualifiedNamespace = client.FullyQualifiedNamespace;
        EntityPath = entityPath;
        ReceiveMode = options.ReceiveMode;
        _prefetchCount = options.PrefetchCount;
        _defaultMaxWaitTime = client.DefaultMaxWaitTime;
        _identifier = options.Identifier ?? Guid.NewGuid().ToString();
        _getStore = getStore;
        Provider = client.Provider;
        _scope = new(Provider.GetNamespaceNameFromHostname(client.FullyQualifiedNamespace), EntityPath, isTopicSubscription);
    }

    public static InMemoryServiceBusReceiver FromQueue(InMemoryServiceBusQueue queue, ServiceBusClientOptions? options = null)
    {
        var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace, options);
        return new(client, queue.QueueName);
    }

    public static InMemoryServiceBusReceiver FromSubscription(InMemoryServiceBusSubscription subscription, ServiceBusClientOptions? options = null)
    {
        var client = InMemoryServiceBusClient.FromNamespace(subscription.Topic.Namespace, options);
        return new(client, subscription.TopicName, subscription.SubscriptionName);
    }

    #endregion

    public InMemoryServiceBusProvider Provider { get; }

    #region Properties

    public override string FullyQualifiedNamespace { get; }
    public override string EntityPath { get; }
    public override ServiceBusReceiveMode ReceiveMode { get; }
    public override int PrefetchCount => _prefetchCount;
    public override string Identifier => _identifier;
    public override bool IsClosed => _isClosed;

    #endregion

    #region Close & Dispose

    public override async Task CloseAsync(CancellationToken cancellationToken = default) => await DisposeAsync();

    public override async ValueTask DisposeAsync()
    {
        await Task.Yield();
        _isClosed = true;
    }

    #endregion

    #region ReceiveMessagesAsync

    public override async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesAsync(int maxMessages, TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return await ReceiveMessagesCoreAsync(maxMessages, maxWaitTime, cancellationToken);
    }

    private async Task<IReadOnlyList<ServiceBusReceivedMessage>> ReceiveMessagesCoreAsync(int maxMessages, TimeSpan? maxWaitTime, CancellationToken cancellationToken)
    {
        var beforeContext = new ReceiveBatchBeforeHookContext(_scope, Provider, cancellationToken);

        await ExecuteBeforeHooksAsync(beforeContext);

        var messages = await _getStore().ReceiveAsync(maxMessages, maxWaitTime ?? _defaultMaxWaitTime, ReceiveMode, cancellationToken);

        var afterContext = new ReceiveBatchAfterHookContext(beforeContext)
        {
            Messages = messages
        };

        await ExecuteAfterHooksAsync(afterContext);

        return messages;
    }

    public override async Task<ServiceBusReceivedMessage?> ReceiveMessageAsync(TimeSpan? maxWaitTime = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return await ReceiveMessageCoreAsync(maxWaitTime, cancellationToken);
    }

    private async Task<ServiceBusReceivedMessage?> ReceiveMessageCoreAsync(TimeSpan? maxWaitTime, CancellationToken cancellationToken)
    {
        var beforeContext = new ReceiveMessageBeforeHookContext(_scope, Provider, cancellationToken);

        await ExecuteBeforeHooksAsync(beforeContext);

        var message = await ServiceBusClientUtils.ReceiveSingleAsync(_getStore(), maxWaitTime ?? _defaultMaxWaitTime, ReceiveMode, cancellationToken);

        var afterContext = new ReceiveMessageAfterHookContext(beforeContext)
        {
            Message = message
        };

        await ExecuteAfterHooksAsync(afterContext);

        return message;
    }

    public override async IAsyncEnumerable<ServiceBusReceivedMessage> ReceiveMessagesAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var beforeContext = new ReceiveBatchBeforeHookContext(_scope, Provider, cancellationToken);

        await ExecuteBeforeHooksAsync(beforeContext);

        var messages = new List<ServiceBusReceivedMessage>();

        await foreach (var item in ServiceBusClientUtils.ReceiveAsAsyncEnumerable(_getStore(), ReceiveMode, cancellationToken))
        {
            messages.Add(item);
            yield return item;
        }

        var afterContext = new ReceiveBatchAfterHookContext(beforeContext)
        {
            Messages = messages
        };

        await ExecuteAfterHooksAsync(afterContext);
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

        _getStore().AbandonMessage(message);
    }

    public override async Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        if (!_getStore().CompleteMessage(message))
        {
            throw ServiceBusExceptionFactory.MessageLockLost(FullyQualifiedNamespace, EntityPath);
        }
    }

    public override async Task RenewMessageLockAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        if (!_getStore().RenewMessageLock(message))
        {
            throw ServiceBusExceptionFactory.MessageLockLost(FullyQualifiedNamespace, EntityPath);
        }
    }

    #endregion

    private Task ExecuteBeforeHooksAsync<TContext>(TContext context) where TContext : ConsumerBeforeHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

    private Task ExecuteAfterHooksAsync<TContext>(TContext context) where TContext : ConsumerAfterHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }



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
