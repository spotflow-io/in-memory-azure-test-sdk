namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

internal interface IConsumerOperation
{

    string ConsumerGroup { get; }
    string PartitionId { get; }
    ConsumerOperations Operation { get; }
}

