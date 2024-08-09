namespace Spotflow.InMemory.Azure.KeyVault.Hooks.Contexts;

public abstract class KeyVaultAfterHookContext(KeyVaultBeforeHookContext before)
    : KeyVaultHookContext(before.VaultName, before.ResourceProvider, before.CancellationToken);
