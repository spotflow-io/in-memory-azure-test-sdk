namespace Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Contexts;

public class GetSecretBeforeHookContext(SecretScope scope, InMemoryKeyVaultProvider provider, CancellationToken cancellationToken)
    : SecretBeforeHookContext(scope, SecretOperations.GetSecret, provider, cancellationToken)
{
    public required string? RequestedSecretVersion { get; init; }
}
