namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

internal interface IConsumerOperation
{
    string ConsumerGroup { get; }
    ConsumerOperations Operation { get; }
}
