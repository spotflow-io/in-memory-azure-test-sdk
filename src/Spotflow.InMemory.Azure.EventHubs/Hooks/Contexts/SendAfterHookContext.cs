using Azure.Messaging.EventHubs;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class SendAfterHookContext(SendBeforeHookContext before) : ProducerAfterHookContext(before)
{
    public required IReadOnlyList<EventData> EventBatch { get; init; }
    public required string PartitionId { get; init; }

    public SendBeforeHookContext BeforeContext => before;

}
