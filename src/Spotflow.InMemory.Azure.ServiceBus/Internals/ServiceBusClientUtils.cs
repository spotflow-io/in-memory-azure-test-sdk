using System.Runtime.CompilerServices;

using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus.Internals;

internal static class ServiceBusClientUtils
{
    public static async IAsyncEnumerable<ServiceBusReceivedMessage> ReceiveAsAsyncEnumerable(SessionlessEngine store, ServiceBusReceiveMode receiveMode, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        while (!cancellationToken.IsCancellationRequested)
        {
            var messages = await store.ReceiveAsync(16, TimeSpan.FromSeconds(8), receiveMode, cancellationToken);


            foreach (var message in messages)
            {
                yield return message;
            }
        }
    }

    public static async Task<ServiceBusReceivedMessage?> ReceiveSingleAsync(SessionlessEngine store, TimeSpan maxWaitTime, ServiceBusReceiveMode receiveMode, CancellationToken cancellationToken)
    {
        var messages = await store.ReceiveAsync(1, maxWaitTime, receiveMode, cancellationToken);
        return messages.SingleOrDefault();
    }

    public static async IAsyncEnumerable<ServiceBusReceivedMessage> ReceiveAsAsyncEnumerable(LockedSession session, ServiceBusReceiveMode receiveMode, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Yield();

        while (!cancellationToken.IsCancellationRequested)
        {
            var result = await session.Store.ReceiveAsync(session, 16, TimeSpan.FromSeconds(8), receiveMode, cancellationToken);

            if (!result.IsSuccessful)
            {
                throw ServiceBusExceptionFactory.SessionReceiveFailed(result.Error.Value, session.FullyQualifiedNamespace, session.EntityPath, session.SessionId);
            }

            foreach (var message in result.Value)
            {
                yield return message;
            }
        }
    }




    public static async Task<ServiceBusReceivedMessage?> ReceiveSingleAsync(LockedSession session, TimeSpan maxWaitTime, ServiceBusReceiveMode receiveMode, CancellationToken cancellationToken)
    {
        var result = await session.Store.ReceiveAsync(session, 1, maxWaitTime, receiveMode, cancellationToken);

        if (!result.IsSuccessful)
        {
            throw ServiceBusExceptionFactory.SessionReceiveFailed(result.Error.Value, session.FullyQualifiedNamespace, session.EntityPath, session.SessionId);
        }

        return result.Value.SingleOrDefault();
    }

    public static InMemoryServiceBusTopic GetTopic(string fullyQualifiedNamespace, string topicName, InMemoryServiceBusProvider provider)
    {
        var ns = GetNamespace(fullyQualifiedNamespace, provider);
        var topic = ns.FindTopic(topicName);

        if (topic is null)
        {
            throw ServiceBusExceptionFactory.MessagingEntityNotFound(fullyQualifiedNamespace, topicName);
        }

        return topic;
    }

    public static InMemoryServiceBusQueue GetQueue(string fullyQualifiedNamespace, string queueName, InMemoryServiceBusProvider provider)
    {
        var ns = GetNamespace(fullyQualifiedNamespace, provider);
        var queue = ns.FindQueue(queueName);

        if (queue is null)
        {
            throw ServiceBusExceptionFactory.MessagingEntityNotFound(fullyQualifiedNamespace, queueName);
        }

        return queue;
    }

    public static InMemoryServiceBusEntity GetEntity(string fullyQualifiedNamespace, string entityName, InMemoryServiceBusProvider provider)
    {
        var ns = GetNamespace(fullyQualifiedNamespace, provider);
        var entity = ns.FindEntity(entityName);

        if (entity is null)
        {
            throw ServiceBusExceptionFactory.MessagingEntityNotFound(fullyQualifiedNamespace, entityName);
        }

        return entity;
    }

    public static InMemoryServiceBusNamespace GetNamespace(string fullyQualifiedNamespace, InMemoryServiceBusProvider provider)
    {
        if (!provider.TryGetFullyQualifiedNamespace(fullyQualifiedNamespace, out var serviceBusNamespace))
        {
            throw ServiceBusExceptionFactory.NamespaceNotFound(fullyQualifiedNamespace);
        }

        return serviceBusNamespace;
    }

    public static InMemoryServiceBusSubscription GetSubscription(string fullyQualifiedNamespace, string topicName, string subscriptionName, InMemoryServiceBusProvider provider)
    {
        var topic = GetTopic(fullyQualifiedNamespace, topicName, provider);
        var subscription = topic.FindSubscription(subscriptionName);


        if (subscription is null)
        {
            var entityPath = InMemoryServiceBusSubscription.FormatEntityPath(topicName, subscriptionName);
            throw ServiceBusExceptionFactory.MessagingEntityNotFound(fullyQualifiedNamespace, entityPath);
        }

        return subscription;
    }

    public static string GetFullyQualifiedNamespace(string connectionString)
    {
        return ServiceBusConnectionStringUtils.GetFullyQualifiedNamespace(connectionString);
    }

}
