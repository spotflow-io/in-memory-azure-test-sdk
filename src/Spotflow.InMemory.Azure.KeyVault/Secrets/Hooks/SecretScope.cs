using Spotflow.InMemory.Azure.KeyVault.Hooks;

namespace Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks;
public record SecretScope(string VaultName, string SecretName) : KeyVaultScope(VaultName);
