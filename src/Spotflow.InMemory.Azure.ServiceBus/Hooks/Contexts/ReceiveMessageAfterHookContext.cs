using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public class ReceiveMessageAfterHookContext(ReceiveMessageBeforeHookContext before) : ConsumerAfterHookContext(before)
{
    public required ServiceBusReceivedMessage? Message { get; init; }
    public ReceiveMessageBeforeHookContext BeforeContext => before;
}
