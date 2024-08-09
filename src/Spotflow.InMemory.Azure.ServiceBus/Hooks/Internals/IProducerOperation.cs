namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Internals;

internal interface IProducerOperation
{
    ProducerOperations Operation { get; }
}
