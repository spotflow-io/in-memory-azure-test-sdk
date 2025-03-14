using Spotflow.InMemory.Azure.KeyVault.Internals;

namespace Spotflow.InMemory.Azure.KeyVault.Resources;

public class InMemoryKeyVault
{
    public InMemoryKeyVault(string vaultName, InMemoryKeyVaultProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vaultName);
        ArgumentNullException.ThrowIfNull(provider);

        Name = vaultName;
        VaultUri = new($"https://{vaultName}.{provider.HostnameSuffix}");
        Provider = provider;
        Secrets = new InMemoryKeyVaultSecretsService(this);
    }

    public string Name { get; }
    public Uri VaultUri { get; }
    public InMemoryKeyVaultProvider Provider { get; }
    internal InMemoryKeyVaultSecretsService Secrets { get; }

}
