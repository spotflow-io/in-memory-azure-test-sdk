namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

internal interface IProducerOperation
{
    ProducerOperations Operation { get; }
    string? TargetPartitionId { get; }
    string? TargetPartitionKey { get; }
}
