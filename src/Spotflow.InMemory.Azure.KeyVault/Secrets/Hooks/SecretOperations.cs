namespace Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks;

[Flags]
public enum SecretOperations
{
    None = 0,
    GetSecret = 1,
    SetSecret = 2,
    All = GetSecret | SetSecret
}
