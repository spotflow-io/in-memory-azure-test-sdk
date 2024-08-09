namespace Spotflow.InMemory.Azure.KeyVault.Hooks.Contexts;

public abstract class KeyVaultHookContext(string vaultName, InMemoryKeyVaultProvider provider, CancellationToken cancellationToken)
{
    public string VaultName => vaultName;

    public InMemoryKeyVaultProvider ResourceProvider => provider;
    public TimeProvider TimeProvider => provider.TimeProvider;
    public CancellationToken CancellationToken => cancellationToken;
}
