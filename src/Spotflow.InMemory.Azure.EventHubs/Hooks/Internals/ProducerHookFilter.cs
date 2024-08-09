using Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

internal record ProducerHookFilter : EventHubHookFilter
{
    public ProducerHookFilter() { }

    public ProducerHookFilter(EventHubHookFilter filter) : base(filter) { }

    public string? TargetPartitionId { get; private init; }
    public string? TargetPartitionKey { get; private init; }

    public ProducerOperations Operations { get; private init; } = ProducerOperations.All;

    public override bool Covers(EventHubHookContext context)
    {
        var result = base.Covers(context);

        if (context is IProducerOperation producer)
        {
            result &= TargetPartitionId is null || TargetPartitionId == producer.TargetPartitionId;
            result &= TargetPartitionKey is null || TargetPartitionKey == producer.TargetPartitionKey;
            result &= Operations.HasFlag(producer.Operation);

            return result;
        }

        throw new InvalidOperationException($"Unexpected context: {context}");
    }

    public ProducerHookFilter With(ProducerOperations? operations) => this with { Operations = operations ?? Operations };
}

