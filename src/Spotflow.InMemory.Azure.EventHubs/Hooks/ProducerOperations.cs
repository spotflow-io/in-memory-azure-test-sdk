namespace Spotflow.InMemory.Azure.EventHubs.Hooks;

[Flags]
public enum ProducerOperations
{
    None = 0,
    Send = 1,
    All = Send
}

