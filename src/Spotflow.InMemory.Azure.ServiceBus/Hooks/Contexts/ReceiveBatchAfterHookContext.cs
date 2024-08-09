using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public class ReceiveBatchAfterHookContext(ReceiveBatchBeforeHookContext before) : ConsumerAfterHookContext(before)
{
    public required IReadOnlyList<ServiceBusReceivedMessage> Messages { get; init; }
    public ReceiveBatchBeforeHookContext BeforeContext => before;
}
