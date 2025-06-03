using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Internals;
internal class InMemoryServiceBusSessionProcessor : ServiceBusSessionProcessor
{
    internal InMemoryServiceBusSessionProcessor(ServiceBusClient client, string queueName, ServiceBusSessionProcessorOptions options) : base(client, queueName, options)
    {
    }

    internal InMemoryServiceBusSessionProcessor(ServiceBusClient client, string topicName, string subscriptionName, ServiceBusSessionProcessorOptions options) : base(client, topicName, subscriptionName, options)
    {
    }
}
