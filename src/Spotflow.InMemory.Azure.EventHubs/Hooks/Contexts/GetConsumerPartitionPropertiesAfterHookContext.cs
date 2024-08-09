using Azure.Messaging.EventHubs;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class GetConsumerPartitionPropertiesAfterHookContext(GetConsumerPartitionPropertiesBeforeHookContext before) : ConsumerPartitionAfterHookContext(before)
{
    public required PartitionProperties PartitionProperties { get; init; }
    public GetConsumerPartitionPropertiesBeforeHookContext BeforeContext => before;
}
