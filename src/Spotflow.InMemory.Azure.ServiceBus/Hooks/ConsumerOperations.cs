namespace Spotflow.InMemory.Azure.ServiceBus.Hooks;

[Flags]
public enum ConsumerOperations
{
    None = 0,
    ReceiveMessage = 1,
    ReceiveBatch = 2,
    All = ReceiveMessage | ReceiveBatch
}
