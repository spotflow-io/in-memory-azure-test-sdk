namespace Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Internals;

internal interface ISecretOperation
{
    SecretOperations Operation { get; }
    string SecretName { get; }
}



