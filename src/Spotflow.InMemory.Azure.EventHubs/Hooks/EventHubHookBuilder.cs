using Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;
using Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;
using Spotflow.InMemory.Azure.Hooks;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks;

public class EventHubHookBuilder
{
    private readonly EventHubHookFilter _filter;

    internal EventHubHookBuilder(EventHubHookFilter? filter = null)
    {
        _filter = filter ?? new();
    }
    public ProducerHookBuilder ForProducer(string? eventHubNamespaceName = null, string? eventHubName = null)
    {
        var producerFilter = new ProducerHookFilter(_filter.With(eventHubNamespaceName, eventHubName));
        return new(producerFilter);
    }

    public ConsumerHookBuilder ForConsumer(string? eventHubNamespaceName = null, string? eventHubName = null, string? consumerGroupName = null, string? partitionId = null)
    {
        var consumerFilter = new ConsumerHookFilter(_filter.With(eventHubNamespaceName, eventHubName), consumerGroupName, partitionId);
        return new(consumerFilter);
    }

    public EventHubHook<EventHubBeforeHookContext> Before(HookFunc<EventHubBeforeHookContext> hook, string? eventHubNamespaceName = null, string? eventHubName = null)
    {
        return new(hook, _filter.With(eventHubNamespaceName, eventHubName));
    }

    public EventHubHook<EventHubAfterHookContext> After(HookFunc<EventHubAfterHookContext> hook, string? eventHubNamespaceName = null, string? eventHubName = null)
    {
        return new(hook, _filter.With(eventHubNamespaceName, eventHubName));
    }

    public class ProducerHookBuilder
    {
        private readonly ProducerHookFilter _filter;

        internal ProducerHookBuilder(ProducerHookFilter? filter = null)
        {
            _filter = filter ?? new();
        }

        public EventHubHook<ProducerBeforeHookContext> Before(HookFunc<ProducerBeforeHookContext> hook, ProducerOperations? operations = null) => new(hook, _filter.With(operations));

        public EventHubHook<ProducerAfterHookContext> After(HookFunc<ProducerAfterHookContext> hook, ProducerOperations? operations = null) => new(hook, _filter.With(operations));

        public EventHubHook<SendBeforeHookContext> BeforeSend(HookFunc<SendBeforeHookContext> hook) => new(hook, _filter);

        public EventHubHook<SendAfterHookContext> AfterSend(HookFunc<SendAfterHookContext> hook) => new(hook, _filter);
    }

    public class ConsumerHookBuilder
    {
        private readonly ConsumerHookFilter _filter;

        internal ConsumerHookBuilder(ConsumerHookFilter? filter)
        {
            _filter = filter ?? new();
        }

        public EventHubHook<ConsumerBeforeHookContext> Before(HookFunc<ConsumerBeforeHookContext> hook, ConsumerOperations? operations = null) => new(hook, _filter.With(operations));

        public EventHubHook<ConsumerAfterHookContext> After(HookFunc<ConsumerAfterHookContext> hook, ConsumerOperations? operations = null) => new(hook, _filter.With(operations));

        public EventHubHook<ReceiveBatchBeforeHookContext> BeforeReceiveBatch(HookFunc<ReceiveBatchBeforeHookContext> hook) => new(hook, _filter);

        public EventHubHook<ReceiveBatchAfterHookContext> AfterReceiveBatch(HookFunc<ReceiveBatchAfterHookContext> hook) => new(hook, _filter);

        public EventHubHook<GetEventHubPropertiesConsumerBeforeHookContext> BeforeGetEventHubProperties(HookFunc<GetEventHubPropertiesConsumerBeforeHookContext> hook) => new(hook, _filter);

        public EventHubHook<GetEventHubPropertiesConsumerAfterHookContext> AfterGetEventHubProperties(HookFunc<GetEventHubPropertiesConsumerAfterHookContext> hook) => new(hook, _filter);

        public EventHubHook<GetPartitionPropertiesConsumerBeforeHookContext> BeforeGetPartitionProperties(HookFunc<GetPartitionPropertiesConsumerBeforeHookContext> hook) => new(hook, _filter);

        public EventHubHook<GetPartitionPropertiesConsumerAfterHookContext> AfterGetPartitionProperties(HookFunc<GetPartitionPropertiesConsumerAfterHookContext> hook) => new(hook, _filter);
    }
}
