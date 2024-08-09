using Azure.Security.KeyVault.Secrets;

namespace Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Contexts;

public class GetSecretAfterHookContext(GetSecretBeforeHookContext before) : SecretAfterHookContext(before)
{
    public GetSecretBeforeHookContext BeforeContext => before;

    public required KeyVaultSecret Secret { get; init; }
}
