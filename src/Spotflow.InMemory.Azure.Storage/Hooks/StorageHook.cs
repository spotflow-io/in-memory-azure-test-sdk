using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;
using Spotflow.InMemory.Azure.Storage.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Hooks;

public class StorageHook<TContext> where TContext : StorageHookContext
{
    internal StorageHook(HookFunc<TContext> hookFunction, StorageHookFilter filter)
    {
        HookFunction = hookFunction;
        Filter = filter;
    }

    internal HookFunc<TContext> HookFunction { get; }
    internal StorageHookFilter Filter { get; }
}
