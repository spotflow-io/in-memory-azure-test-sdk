using Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Internals;

internal record ProducerHookFilter : ServiceBusHookFilter
{
    public ProducerHookFilter(ServiceBusHookFilter filter) : base(filter) { }

    public ProducerOperations Operations { get; private init; } = ProducerOperations.All;

    public override bool Covers(ServiceBusHookContext context)
    {
        var result = base.Covers(context);

        if (context is IProducerOperation producer)
        {
            result &= Operations.HasFlag(producer.Operation);
            return result;
        }

        throw new InvalidOperationException($"Unexpected context: {context}");
    }

    public ProducerHookFilter With(ProducerOperations? operations)
    {
        return new(this)
        {
            Operations = operations ?? Operations
        };
    }
}
