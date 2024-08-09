using Spotflow.InMemory.Azure.KeyVault.Hooks.Contexts;
using Spotflow.InMemory.Azure.KeyVault.Hooks.Internals;

namespace Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Internals;

internal record SecretHookFilter : KeyVaultHookFilter
{
    public SecretHookFilter(KeyVaultHookFilter filter) : base(filter) { }

    public string? SecretName { get; private set; }

    public SecretOperations Operations { get; private set; } = SecretOperations.All;

    public override bool Covers(KeyVaultHookContext context)
    {
        var result = base.Covers(context);

        if (context is ISecretOperation secret)
        {
            result &= SecretName is null || secret.SecretName == SecretName;
            result &= Operations.HasFlag(secret.Operation);

            return result;
        }

        throw new InvalidOperationException($"Unexpected context: {context}");
    }

    internal SecretHookFilter With(string? secretName = null, SecretOperations? operations = null)
    {
        return this with
        {
            SecretName = secretName ?? SecretName,
            Operations = operations ?? Operations
        };
    }

}



