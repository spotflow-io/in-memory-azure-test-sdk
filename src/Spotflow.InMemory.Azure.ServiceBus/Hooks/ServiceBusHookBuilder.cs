using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;
using Spotflow.InMemory.Azure.ServiceBus.Hooks.Internals;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks;

public class ServiceBusHookBuilder
{
    private readonly ServiceBusHookFilter _filter;

    internal ServiceBusHookBuilder(ServiceBusHookFilter? filter = null)
    {
        _filter = filter ?? new();
    }

    public ProducerHookBuilder ForProducer(string? serviceBusNamespaceName = null, string? entityPath = null)
    {
        return new(_filter.With(serviceBusNamespaceName, entityPath));
    }
    public ConsumerHookBuilder ForConsumer(string? serviceBusNamespaceName = null, string? entityPath = null, bool? isTopicSubscription = null)
    {
        return new(_filter.With(serviceBusNamespaceName, entityPath), isTopicSubscription);
    }
    public ServiceBusHook<ServiceBusBeforeHookContext> Before(HookFunc<ServiceBusBeforeHookContext> hook, string? serviceBusNamespaceName = null, string? entityPath = null)
    {
        return new(hook, _filter.With(serviceBusNamespaceName, entityPath));
    }
    public ServiceBusHook<ServiceBusAfterHookContext> After(HookFunc<ServiceBusAfterHookContext> hook, string? serviceBusNamespaceName = null, string? entityPath = null)
    {
        return new(hook, _filter.With(serviceBusNamespaceName, entityPath));
    }


    public class ProducerHookBuilder
    {
        private readonly ProducerHookFilter _filter;

        internal ProducerHookBuilder(ServiceBusHookFilter filter)
        {
            _filter = new(filter);
        }
        public ServiceBusHook<ProducerBeforeHookContext> Before(HookFunc<ProducerBeforeHookContext> hook, ProducerOperations? operations = null) => new(hook, _filter.With(operations));
        public ServiceBusHook<ProducerAfterHookContext> After(HookFunc<ProducerAfterHookContext> hook, ProducerOperations? operations = null) => new(hook, _filter.With(operations));

        public ServiceBusHook<SendBatchBeforeHookContext> BeforeSendBatch(HookFunc<SendBatchBeforeHookContext> hook) => new(hook, _filter);

        public ServiceBusHook<SendBatchAfterHookContext> AfterSendBatch(HookFunc<SendBatchAfterHookContext> hook) => new(hook, _filter);

        public ServiceBusHook<SendMessageBeforeHookContext> BeforeSendMessage(HookFunc<SendMessageBeforeHookContext> hook) => new(hook, _filter);

        public ServiceBusHook<SendMessageAfterHookContext> AfterSendMessage(HookFunc<SendMessageAfterHookContext> hook) => new(hook, _filter);
    }

    public class ConsumerHookBuilder
    {
        private readonly ConsumerHookFilter _filter;

        internal ConsumerHookBuilder(ServiceBusHookFilter filter, bool? isTopicSubscription)
        {
            _filter = new(filter, isTopicSubscription);
        }

        public ServiceBusHook<ConsumerBeforeHookContext> Before(HookFunc<ConsumerBeforeHookContext> hook, ConsumerOperations? operations = null) => new(hook, _filter.With(operations));
        public ServiceBusHook<ConsumerAfterHookContext> After(HookFunc<ConsumerAfterHookContext> hook, ConsumerOperations? operations = null) => new(hook, _filter.With(operations));

        public ServiceBusHook<ReceiveBatchBeforeHookContext> BeforeReceiveBatch(HookFunc<ReceiveBatchBeforeHookContext> hook) => new(hook, _filter);

        public ServiceBusHook<ReceiveBatchAfterHookContext> AfterReceiveBatch(HookFunc<ReceiveBatchAfterHookContext> hook) => new(hook, _filter);

        public ServiceBusHook<ReceiveMessageBeforeHookContext> BeforeReceiveMessage(HookFunc<ReceiveMessageBeforeHookContext> hook) => new(hook, _filter);

        public ServiceBusHook<ReceiveMessageAfterHookContext> AfterReceiveMessage(HookFunc<ReceiveMessageAfterHookContext> hook) => new(hook, _filter);
    }
}
