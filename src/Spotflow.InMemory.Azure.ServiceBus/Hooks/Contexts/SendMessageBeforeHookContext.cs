using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public class SendMessageBeforeHookContext(ServiceBusProducerHookScope scope, InMemoryServiceBusProvider provider, CancellationToken cancellationToken)
    : ProducerBeforeHookContext(scope, ProducerOperations.SendMessage, provider, cancellationToken)
{
    public required ServiceBusMessage Message { get; init; }
}
