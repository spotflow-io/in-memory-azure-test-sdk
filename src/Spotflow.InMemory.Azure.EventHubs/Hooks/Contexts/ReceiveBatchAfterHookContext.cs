using Azure.Messaging.EventHubs;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class ReceiveBatchAfterHookContext(ReceiveBatchBeforeHookContext before) : PartitionConsumerAfterHookContext(before)
{
    public required IReadOnlyList<EventData> EventBatch { get; init; }
    public ReceiveBatchBeforeHookContext BeforeContext => before;
}
