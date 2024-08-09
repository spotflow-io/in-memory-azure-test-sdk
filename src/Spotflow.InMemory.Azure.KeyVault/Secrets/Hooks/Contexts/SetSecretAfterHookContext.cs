using Azure.Security.KeyVault.Secrets;

namespace Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Contexts;

public class SetSecretAfterHookContext(SetSecretBeforeHookContext before) : SecretAfterHookContext(before)
{
    public SetSecretBeforeHookContext BeforeContext => before;

    public required KeyVaultSecret CreatedSecret { get; init; }
}
