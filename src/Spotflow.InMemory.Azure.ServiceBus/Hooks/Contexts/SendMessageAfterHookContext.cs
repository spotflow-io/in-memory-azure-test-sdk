using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

public class SendMessageAfterHookContext(SendMessageBeforeHookContext before) : ProducerAfterHookContext(before)
{
    public required ServiceBusMessage Message { get; init; }
    public SendMessageBeforeHookContext BeforeContext => before;
}
