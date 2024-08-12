namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

internal interface IConsumerPartitionOperation : IConsumerOperation
{

    string PartitionId { get; }
}
