namespace Spotflow.InMemory.Azure.EventHubs.Hooks;

[Flags]
public enum ConsumerOperations
{
    None = 0,
    ReceiveBatch = 1,
    All = ReceiveBatch
}

