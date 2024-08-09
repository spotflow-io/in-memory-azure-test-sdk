using Azure.Messaging.EventHubs;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class GetPartitionPropertiesConsumerAfterHookContext(GetPartitionPropertiesConsumerBeforeHookContext before)
    : PartitionConsumerAfterHookContext(before)
{
    public required PartitionProperties PartitionProperties { get; init; }
    public GetPartitionPropertiesConsumerBeforeHookContext BeforeContext => before;
}
