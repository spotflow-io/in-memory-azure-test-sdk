using System.Runtime.CompilerServices;

using Azure;

namespace Spotflow.InMemory.Azure.KeyVault.Internals;

internal static class KeyVaultExceptionFactory
{
    public static NotSupportedException MethodNotSupported([CallerMemberName] string? callerMemberName = null)
    {
        return new($"In-memory key vault client does not support method '{callerMemberName}'.");
    }

    public static NotSupportedException FeatureNotSupported(string featureName)
    {
        return new($"In-memory key vault hub client does not support feature '{featureName}'.");
    }

    public static RequestFailedException KeyVaultNotFound(Uri vaultUri)
    {
        return new($"Key Vault not found: {vaultUri}.");
    }

    public static RequestFailedException SecretVersionNotFound(Uri vaultUri, string secretName, string secretVersion)
    {
        return new(404, $"Secret version '{secretName}/{secretVersion}' not found in vault '{vaultUri}'", "SecretNotFound", null);
    }

    public static RequestFailedException SecretNotFound(Uri vaultUri, string secretName)
    {
        return new(404, $"Secret '{secretName}' not found in vault '{vaultUri}'", "SecretNotFound", null);

    }

    public static RequestFailedException SecretDisabled(Uri vaultUri, string secretName)
    {
        return new(403, $"Operation get is not allowed on a disabled secret. Vault = {vaultUri}, Secret = {secretName}", "Forbidden", null);
    }

    public static RequestFailedException SecretIsDeleted(Uri vaultUri, string secretName)
    {
        return new(409, $"Secret '{secretName} is deleted in vault '{vaultUri}'", "SecretIsDeleted", null);
    }

    public static RequestFailedException VersionNotSpecified(Uri vaultUri, string secretName)
    {
        return new(400, $"Version for secret '{secretName}' not found in vault '{vaultUri}' not specified.", "VersionNotSpecified", null);
    }
}
