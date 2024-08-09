using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public class SendBatchAfterHookContext(SendBatchBeforeHookContext before) : ProducerAfterHookContext(before)
{
    public required IReadOnlyList<ServiceBusMessage> Messages { get; init; }
    public SendBatchBeforeHookContext BeforeContext => before;
}
