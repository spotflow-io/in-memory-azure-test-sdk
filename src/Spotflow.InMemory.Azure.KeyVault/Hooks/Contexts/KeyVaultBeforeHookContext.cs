namespace Spotflow.InMemory.Azure.KeyVault.Hooks.Contexts;

public abstract class KeyVaultBeforeHookContext(KeyVaultScope scope, InMemoryKeyVaultProvider provider, CancellationToken cancellationToken)
    : KeyVaultHookContext(scope.VaultName, provider, cancellationToken);
