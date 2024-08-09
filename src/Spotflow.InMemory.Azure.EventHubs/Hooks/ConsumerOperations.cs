namespace Spotflow.InMemory.Azure.EventHubs.Hooks;

[Flags]
public enum ConsumerOperations
{
    None = 0,
    ReceiveBatch = 1,
    GetEventHubProperties = 2,
    GetPartitionProperties = 4,
    All = ReceiveBatch | GetEventHubProperties | GetPartitionProperties
}

