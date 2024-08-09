using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public class SendBatchBeforeHookContext(ServiceBusProducerHookScope scope, InMemoryServiceBusProvider provider, CancellationToken cancellationToken)
    : ProducerBeforeHookContext(scope, ProducerOperations.SendBatch, provider, cancellationToken)
{
    public required IReadOnlyList<ServiceBusMessage> Messages { get; init; }
}
