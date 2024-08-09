using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.KeyVault.Hooks.Contexts;
using Spotflow.InMemory.Azure.KeyVault.Hooks.Internals;
using Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks;
using Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Contexts;
using Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Internals;

namespace Spotflow.InMemory.Azure.KeyVault.Hooks;

public class KeyVaultHookBuilder
{
    private readonly KeyVaultHookFilter _filter;

    internal KeyVaultHookBuilder(KeyVaultHookFilter? filter = null)
    {
        _filter = filter ?? new();
    }

    public SecretsHookBuilder ForSecrets(string? vaultName = null) => new(_filter.With(vaultName));

    public KeyVaultHook<KeyVaultBeforeHookContext> Before(HookFunc<KeyVaultBeforeHookContext> hook, string? vaultName = null)
    {
        return new(hook, _filter.With(vaultName));
    }

    public KeyVaultHook<KeyVaultAfterHookContext> After(HookFunc<KeyVaultAfterHookContext> hook, string? vaultName = null)
    {
        return new(hook, _filter.With(vaultName));
    }

    public class SecretsHookBuilder
    {
        private readonly SecretHookFilter _filter;

        internal SecretsHookBuilder(KeyVaultHookFilter filter)
        {
            _filter = new(filter);
        }

        public KeyVaultHook<SecretBeforeHookContext> Before(HookFunc<SecretBeforeHookContext> hook, SecretOperations? operations = null) => new(hook, _filter.With(operations: operations));
        public KeyVaultHook<SecretAfterHookContext> After(HookFunc<SecretAfterHookContext> hook, SecretOperations? operations = null) => new(hook, _filter.With(operations: operations));

        public KeyVaultHook<GetSecretBeforeHookContext> BeforeGetSecret(HookFunc<GetSecretBeforeHookContext> hook) => new(hook, _filter);

        public KeyVaultHook<GetSecretAfterHookContext> AfterGetSecret(HookFunc<GetSecretAfterHookContext> hook) => new(hook, _filter);

        public KeyVaultHook<SetSecretBeforeHookContext> BeforeSetSecret(HookFunc<SetSecretBeforeHookContext> hook) => new(hook, _filter);

        public KeyVaultHook<SetSecretAfterHookContext> AfterSetSecret(HookFunc<SetSecretAfterHookContext> hook) => new(hook, _filter);
    }

}
