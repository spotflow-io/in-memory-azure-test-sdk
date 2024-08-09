namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Internals;

internal interface IConsumerOperation
{
    ConsumerOperations Operation { get; }
    bool IsTopicSubscription { get; }
}
