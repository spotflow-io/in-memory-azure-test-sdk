using Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

internal record ConsumerHookFilter : EventHubHookFilter
{
    public ConsumerHookFilter() { }

    public ConsumerHookFilter(EventHubHookFilter filter, string? consumerGroup, string? partitionId) : base(filter)
    {
        ConsumerGroup = consumerGroup;
        PartitionId = partitionId;
    }

    public string? ConsumerGroup { get; private init; }
    public string? PartitionId { get; private init; }

    public ConsumerOperations Operations { get; private init; } = ConsumerOperations.All;

    public override bool Covers(EventHubHookContext context)
    {
        var result = base.Covers(context);

        if (context is IConsumerOperation consumer)
        {
            result &= ConsumerGroup is null || ConsumerGroup == consumer.ConsumerGroup;
            result &= Operations.HasFlag(consumer.Operation);

            if (consumer is IConsumerPartitionOperation consumerPartition)
            {
                result &= PartitionId is null || PartitionId == consumerPartition.PartitionId;
            }

            return result;
        }

        throw new InvalidOperationException($"Unexpected context: {context}");
    }

    public EventHubHookFilter With(ConsumerOperations? operations = null, string? partitionId = null)
        => this with { Operations = operations ?? Operations, PartitionId = partitionId ?? PartitionId };

}

