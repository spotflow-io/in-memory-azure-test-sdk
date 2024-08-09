using Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;
using Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;
using Spotflow.InMemory.Azure.Hooks;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks;

public class EventHubHook<TContext> where TContext : EventHubHookContext
{
    public HookFunc<TContext> HookFunction { get; }
    internal EventHubHookFilter Filter { get; }

    internal EventHubHook(HookFunc<TContext> hookFunction, EventHubHookFilter filter)
    {
        HookFunction = hookFunction;
        Filter = filter;
    }
}
