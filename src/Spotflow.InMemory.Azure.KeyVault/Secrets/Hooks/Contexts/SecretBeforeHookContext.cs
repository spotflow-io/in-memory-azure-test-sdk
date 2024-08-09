using Spotflow.InMemory.Azure.KeyVault.Hooks.Contexts;
using Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Internals;

namespace Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Contexts;
public abstract class SecretBeforeHookContext(SecretScope scope, SecretOperations operation, InMemoryKeyVaultProvider provider, CancellationToken cancellationToken)
    : KeyVaultBeforeHookContext(scope, provider, cancellationToken), ISecretOperation
{
    public string SecretName => scope.SecretName;
    public SecretOperations Operation => operation;
}
