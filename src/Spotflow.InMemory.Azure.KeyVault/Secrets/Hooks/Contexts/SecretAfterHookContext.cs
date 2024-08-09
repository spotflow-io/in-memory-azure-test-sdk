using Spotflow.InMemory.Azure.KeyVault.Hooks.Contexts;
using Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Internals;

namespace Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Contexts;

public abstract class SecretAfterHookContext(SecretBeforeHookContext before) : KeyVaultAfterHookContext(before), ISecretOperation
{
    public SecretOperations Operation => before.Operation;
    public string SecretName => before.SecretName;
}
