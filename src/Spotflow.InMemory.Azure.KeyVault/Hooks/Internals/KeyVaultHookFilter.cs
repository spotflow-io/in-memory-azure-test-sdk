using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.KeyVault.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.KeyVault.Hooks.Internals;

internal record KeyVaultHookFilter : BaseHookFilter<KeyVaultHookContext>
{
    public string? VaultName { get; private init; }

    public override bool Covers(KeyVaultHookContext context)
    {
        return VaultName is null || VaultName == context.VaultName;
    }

    public KeyVaultHookFilter With(string? vaultName)
    {
        return this with { VaultName = vaultName ?? VaultName };
    }
}



