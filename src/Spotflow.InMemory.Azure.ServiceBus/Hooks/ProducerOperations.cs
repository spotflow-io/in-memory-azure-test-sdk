namespace Spotflow.InMemory.Azure.ServiceBus.Hooks;

[Flags]
public enum ProducerOperations
{
    None = 0,
    SendMessage = 1,
    SendBatch = 2,
    All = SendMessage | SendBatch
}
