using System.Diagnostics.CodeAnalysis;

using Azure.Core;
using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.Auth;
using Spotflow.InMemory.Azure.ServiceBus.Internals;
using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus;

public class InMemoryServiceBusClient : ServiceBusClient
{

    private volatile bool _isClosed;

    #region Constructors

    public InMemoryServiceBusClient(string connectionString, InMemoryServiceBusProvider provider)
        : this(ServiceBusClientUtils.GetFullyQualifiedNamespace(connectionString), new(), provider, null) { }

    public InMemoryServiceBusClient(string connectionString, ServiceBusClientOptions options, InMemoryServiceBusProvider provider)
        : this(ServiceBusClientUtils.GetFullyQualifiedNamespace(connectionString), options, provider, null) { }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Distinguishing from other constructors")]
    public InMemoryServiceBusClient(string fullyQualifiedNamespace, TokenCredential credential, InMemoryServiceBusProvider provider)
        : this(fullyQualifiedNamespace, new(), provider, null) { }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Distinguishing from other constructors")]
    public InMemoryServiceBusClient(string fullyQualifiedNamespace, TokenCredential credential, ServiceBusClientOptions options, InMemoryServiceBusProvider provider)
        : this(fullyQualifiedNamespace, options, provider, null) { }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Distinguishing from other constructors")]
    private InMemoryServiceBusClient(string fullyQualifiedNamespace, ServiceBusClientOptions options, InMemoryServiceBusProvider provider, object? dummy)
    {
        Provider = provider;
        DefaultMaxWaitTime = options.RetryOptions.MaxDelay;
        TryTimeout = options.RetryOptions.TryTimeout;
        FullyQualifiedNamespace = fullyQualifiedNamespace;
        Identifier = options.Identifier ?? Guid.NewGuid().ToString();
    }

    public static InMemoryServiceBusClient FromNamespace(InMemoryServiceBusNamespace serviceBusNamespace, ServiceBusClientOptions? options = null)
    {
        return new(serviceBusNamespace.FullyQualifiedNamespace, NoOpTokenCredential.Instance, options ?? new(), serviceBusNamespace.Provider);
    }

    #endregion

    public InMemoryServiceBusProvider Provider { get; }

    internal TimeSpan DefaultMaxWaitTime { get; }
    internal TimeSpan TryTimeout { get; }

    #region Properties
    public override string FullyQualifiedNamespace { get; }

    public override bool IsClosed => _isClosed;

    public override string Identifier { get; }

    #endregion

    #region Dispose

    public override async ValueTask DisposeAsync()
    {
        await Task.Yield();

        _isClosed = true;

    }

    #endregion

    #region CreateSender

    public override InMemoryServiceBusSender CreateSender(string queueOrTopicName) => CreateSender(queueOrTopicName, new());

    public override InMemoryServiceBusSender CreateSender(string queueOrTopicName, ServiceBusSenderOptions options)
    {
        return new InMemoryServiceBusSender(this, queueOrTopicName, options);
    }

    #endregion

    #region CreateReceiver

    public override InMemoryServiceBusReceiver CreateReceiver(string queueName) => CreateReceiver(queueName, new ServiceBusReceiverOptions());

    public override InMemoryServiceBusReceiver CreateReceiver(string queueName, ServiceBusReceiverOptions options)
    {
        return new InMemoryServiceBusReceiver(this, queueName, options);
    }

    public override InMemoryServiceBusReceiver CreateReceiver(string topicName, string subscriptionName) => CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions());

    public override InMemoryServiceBusReceiver CreateReceiver(string topicName, string subscriptionName, ServiceBusReceiverOptions options)
    {
        return new InMemoryServiceBusReceiver(this, topicName, subscriptionName, options);
    }

    #endregion

    #region AcceptNextSession

    public override async Task<ServiceBusSessionReceiver> AcceptNextSessionAsync(string queueName, ServiceBusSessionReceiverOptions? options = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var queue = ServiceBusClientUtils.GetQueue(FullyQualifiedNamespace, queueName, Provider);

        if (queue.Engine is not SessionEngine store)
        {
            throw ServiceBusExceptionFactory.SessionsNotEnabled(FullyQualifiedNamespace, queue.EntityPath);
        }

        var session = await store.TryAcquireNextAvailableSessionAsync(TryTimeout, cancellationToken);

        if (session is null)
        {
            throw ServiceBusExceptionFactory.NoSessionAvailable(FullyQualifiedNamespace, queue.EntityPath);
        }

        return new InMemoryServiceBusSessionReceiver(session, options ?? new(), DefaultMaxWaitTime, Provider);
    }

    public override async Task<ServiceBusSessionReceiver> AcceptNextSessionAsync(string topicName, string subscriptionName, ServiceBusSessionReceiverOptions? options = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var subscription = ServiceBusClientUtils.GetSubscription(FullyQualifiedNamespace, topicName, subscriptionName, Provider);


        if (subscription.Engine is not SessionEngine store)
        {
            throw ServiceBusExceptionFactory.SessionsNotEnabled(FullyQualifiedNamespace, subscription.EntityPath);
        }

        var session = await store.TryAcquireNextAvailableSessionAsync(TryTimeout, cancellationToken);

        if (session is null)
        {
            throw ServiceBusExceptionFactory.NoSessionAvailable(FullyQualifiedNamespace, subscription.EntityPath);
        }

        return new InMemoryServiceBusSessionReceiver(session, options ?? new(), DefaultMaxWaitTime, Provider);
    }

    #endregion

    #region AcceptSession
    public override async Task<ServiceBusSessionReceiver> AcceptSessionAsync(string queueName, string sessionId, ServiceBusSessionReceiverOptions? options = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var queue = ServiceBusClientUtils.GetQueue(FullyQualifiedNamespace, queueName, Provider);

        if (queue.Engine is not SessionEngine store)
        {
            throw ServiceBusExceptionFactory.SessionsNotEnabled(FullyQualifiedNamespace, queue.EntityPath);
        }

        if (!store.TryAcquireSession(sessionId, out var session))
        {
            throw ServiceBusExceptionFactory.SessionNotFound(FullyQualifiedNamespace, queue.EntityPath, sessionId);
        }

        return new InMemoryServiceBusSessionReceiver(session, options ?? new(), DefaultMaxWaitTime, Provider);

    }

    public override async Task<ServiceBusSessionReceiver> AcceptSessionAsync(string topicName, string subscriptionName, string sessionId, ServiceBusSessionReceiverOptions? options = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var subscription = ServiceBusClientUtils.GetSubscription(FullyQualifiedNamespace, topicName, subscriptionName, Provider);

        if (subscription.Engine is not SessionEngine store)
        {
            throw ServiceBusExceptionFactory.SessionsNotEnabled(FullyQualifiedNamespace, subscription.EntityPath);
        }

        if (!store.TryAcquireSession(sessionId, out var session))
        {
            throw ServiceBusExceptionFactory.SessionNotFound(FullyQualifiedNamespace, subscription.EntityPath, sessionId);
        }

        return new InMemoryServiceBusSessionReceiver(session, options ?? new(), DefaultMaxWaitTime, Provider);

    }

    #endregion

    #region CreateProcessors

    public override InMemoryServiceBusProcessor CreateProcessor(string queueName)
        => new(this, queueName);

    public override InMemoryServiceBusProcessor CreateProcessor(string queueName, ServiceBusProcessorOptions options)
        => new(this, queueName, options);

    public override InMemoryServiceBusProcessor CreateProcessor(string topicName, string subscriptionName)
        => new(this, topicName, subscriptionName);

    public override InMemoryServiceBusProcessor CreateProcessor(string topicName, string subscriptionName, ServiceBusProcessorOptions options)
        => new(this, topicName, subscriptionName, options);

    #endregion

    #region Unsupported

    public override ServiceBusSessionProcessor CreateSessionProcessor(string queueName, ServiceBusSessionProcessorOptions? options = null) => throw ServiceBusExceptionFactory.MethodNotSupported();

    public override ServiceBusSessionProcessor CreateSessionProcessor(string topicName, string subscriptionName, ServiceBusSessionProcessorOptions? options = null) => throw ServiceBusExceptionFactory.MethodNotSupported();

    public override ServiceBusRuleManager CreateRuleManager(string topicName, string subscriptionName) => throw ServiceBusExceptionFactory.MethodNotSupported();

    #endregion
}
