using Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Internals;

internal record ConsumerHookFilter : ServiceBusHookFilter
{
    public ConsumerHookFilter(ServiceBusHookFilter filter, bool? isTopicSubscription) : base(filter)
    {
        IsTopicSubscription = isTopicSubscription;
    }

    public ConsumerOperations Operations { get; private init; } = ConsumerOperations.All;
    public bool? IsTopicSubscription { get; }

    public override bool Covers(ServiceBusHookContext context)
    {
        var result = base.Covers(context);

        if (context is IConsumerOperation producer)
        {
            result &= Operations.HasFlag(producer.Operation);
            result &= IsTopicSubscription == null || producer.IsTopicSubscription == IsTopicSubscription;
            return result;
        }

        throw new InvalidOperationException($"Unexpected context: {context}");
    }

    public ConsumerHookFilter With(ConsumerOperations? operations)
    {
        return this with { Operations = operations ?? Operations };
    }
}
