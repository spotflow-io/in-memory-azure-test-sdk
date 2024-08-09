using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.KeyVault.Hooks.Contexts;
using Spotflow.InMemory.Azure.KeyVault.Hooks.Internals;

namespace Spotflow.InMemory.Azure.KeyVault.Hooks;

public class KeyVaultHook<TContext> where TContext : KeyVaultHookContext
{
    internal KeyVaultHook(HookFunc<TContext> hookFunction, KeyVaultHookFilter filter)
    {
        HookFunction = hookFunction;
        Filter = filter;
    }

    internal HookFunc<TContext> HookFunction { get; }
    internal KeyVaultHookFilter Filter { get; }
}
