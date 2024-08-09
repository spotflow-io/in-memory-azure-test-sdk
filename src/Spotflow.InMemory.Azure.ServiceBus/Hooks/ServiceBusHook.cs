using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;
using Spotflow.InMemory.Azure.ServiceBus.Hooks.Internals;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks;

public class ServiceBusHook<TContext> where TContext : ServiceBusHookContext
{
    internal ServiceBusHook(HookFunc<TContext> hookFunction, ServiceBusHookFilter filter)
    {
        HookFunction = hookFunction;
        Filter = filter;
    }

    public HookFunc<TContext> HookFunction { get; }
    internal ServiceBusHookFilter Filter { get; }
}
