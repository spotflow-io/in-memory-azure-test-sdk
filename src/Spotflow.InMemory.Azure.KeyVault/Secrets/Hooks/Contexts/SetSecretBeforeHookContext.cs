using Azure.Security.KeyVault.Secrets;

namespace Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Contexts;

public class SetSecretBeforeHookContext(SecretScope scope, InMemoryKeyVaultProvider provider, CancellationToken cancellationToken)
    : SecretBeforeHookContext(scope, SecretOperations.SetSecret, provider, cancellationToken)
{
    public required KeyVaultSecret Secret { get; init; }
}
