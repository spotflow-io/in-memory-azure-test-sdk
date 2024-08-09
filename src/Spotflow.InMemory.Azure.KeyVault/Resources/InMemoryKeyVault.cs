using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Azure;
using Azure.Security.KeyVault.Secrets;

using Spotflow.InMemory.Azure.KeyVault.Internals;

namespace Spotflow.InMemory.Azure.KeyVault.Resources;

public class InMemoryKeyVault(string vaultName, InMemoryKeyVaultProvider provider)
{
    private readonly ConcurrentDictionary<string, InMemoryKeyVaultSecret> _secrets = new(ReferenceEqualityComparer.Instance);

    public string Name => vaultName;
    public Uri VaultUri { get; } = new($"https://{vaultName}.{provider.HostnameSuffix}");

    public InMemoryKeyVaultProvider Provider => provider;

    public bool TryGetSecret(string secretName, string? secretVersion, [NotNullWhen(true)] out KeyVaultSecret? result, [NotNullWhen(false)] out GetSecretError? error)
    {
        if (!TryGetActiveSecret(secretName, out var secret))
        {
            result = null;
            error = new GetSecretError.SecretNotFound(VaultUri, secretName);
            return false;
        }

        if (!secret.IsEnabled)
        {
            result = null;
            error = new GetSecretError.SecretDisabled(VaultUri, secretName);
            return false;
        }

        if (secretVersion is null)
        {
            result = secret.GetLastVersion().ToClientModel();
            error = null;
            return true;
        }
        else
        {
            var version = secret.FindVersion(secretVersion);

            if (version is null)
            {
                result = null;
                error = new GetSecretError.VersionNotFound(VaultUri, secretName, secretVersion);
                return false;
            }

            result = version.ToClientModel();
            error = null;
            return true;
        }
    }

    public bool TrySetSecret(
        KeyVaultSecret inputSecretVersion,
        [NotNullWhen(true)] out KeyVaultSecret? result,
        [NotNullWhen(false)] out SetSecretError? error,
        CancellationToken cancellationToken
        )
    {
        if (!ValidateSecret(inputSecretVersion, out var validationError))
        {
            result = null;
            error = validationError;
            return false;
        }

        var secretName = inputSecretVersion.Name;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_secrets.TryGetValue(secretName, out var existingSecret))
            {
                if (existingSecret.IsDeleted)
                {
                    result = null;
                    error = new SetSecretError.SecretIsDeleted(VaultUri, secretName);
                    return false;
                }

                var updatedSecret = existingSecret.WithNewVersion(inputSecretVersion);

                if (_secrets.TryUpdate(secretName, updatedSecret, existingSecret)) // Retry if the secret has been updated since we last read it
                {
                    result = updatedSecret.GetLastVersion().ToClientModel();
                    error = null;
                    return true;
                }
            }
            else
            {
                var newSecret = InMemoryKeyVaultSecret.CreateWithInitialVersion(VaultUri, secretName, inputSecretVersion, provider.TimeProvider);

                if (_secrets.TryAdd(secretName, newSecret)) // Retry if the secret has been added since we last checked
                {
                    result = newSecret.GetLastVersion().ToClientModel();
                    error = null;
                    return true;
                }
            }
        }
    }

    public bool TryListSecrets([NotNullWhen(true)] out IReadOnlyList<SecretProperties>? result, [NotNullWhen(false)] out ListSecretsError? error)
    {
        var items = new List<SecretProperties>(_secrets.Count);

        foreach (var (_, secret) in _secrets)
        {
            if (!secret.IsDeleted)
            {
                items.Add(secret.ToClientListItem());
            }
        }

        error = null;
        result = items;
        return true;
    }

    public bool TryListSecretVersions(
        string secretName,
        [NotNullWhen(true)] out IReadOnlyList<SecretProperties>? result,
        [NotNullWhen(false)] out ListSecretVersionsError? error)
    {
        if (!TryGetActiveSecret(secretName, out var secret))
        {
            result = null;
            error = new ListSecretVersionsError.SecretNotFound(VaultUri, secretName);
            return false;
        }

        result = secret.GetVersions().Select(v => v.ToClientModel().Properties).ToList();
        error = null;
        return true;

    }

    public bool TryStartDeleteSecret(
        string secretName,
        [NotNullWhen(true)] out DeleteSecretOperation? result,
        [NotNullWhen(false)] out StartDeleteSecretError? error,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetActiveSecret(secretName, out var originalSecret))
            {
                result = null;
                error = new StartDeleteSecretError.SecretNotFound(VaultUri, secretName);
                return false;
            }

            var deletedSecret = originalSecret.Deleted();

            if (_secrets.TryUpdate(secretName, deletedSecret, originalSecret))
            {
                result = new InMemoryDeleteSecretOperation(deletedSecret);
                error = null;
                return true;
            }
        }
    }

    public bool TryUpdateSecretVersionProperties(
        SecretProperties properties,
        [NotNullWhen(true)] out SecretProperties? result,
        [NotNullWhen(false)] out UpdateSecretVersionPropertiesError? error,
        CancellationToken cancellationToken)
    {
        var secretName = properties.Name;
        var secretVersion = properties.Version;

        if (string.IsNullOrWhiteSpace(secretVersion))
        {
            error = new UpdateSecretVersionPropertiesError.VersionNotSpecified(VaultUri, secretName);
            result = null;
            return false;
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetActiveSecret(properties.Name, out var originalSecret))
            {
                error = new UpdateSecretVersionPropertiesError.SecretNotFound(VaultUri, secretName);
                result = null;
                return false;
            }

            if (!originalSecret.HasVersion(secretVersion))
            {
                error = new UpdateSecretVersionPropertiesError.VersionNotFound(VaultUri, secretName, secretVersion);
                result = null;
                return false;
            }

            var updatedSecret = originalSecret.WithUpdatedVersionProperties(secretVersion, properties);

            // Retry if the secret has been updated since we last read it
            if (_secrets.TryUpdate(secretName, updatedSecret, originalSecret))
            {
                result = updatedSecret.GetVersion(secretVersion).ToClientModel().Properties;
                error = null;
                return true;
            }

        }
    }

    private bool TryGetActiveSecret(string secretName, [NotNullWhen(true)] out InMemoryKeyVaultSecret? secret)
    {
        if (!_secrets.TryGetValue(secretName, out secret))
        {
            return false;
        }

        if (secret.IsDeleted)
        {
            return false;
        }

        return true;
    }


    private static bool ValidateSecret(KeyVaultSecret inputSecret, [NotNullWhen(false)] out SetSecretError? error)
    {
        if (!Regex.IsMatch(inputSecret.Name, @"^[a-zA-Z0-9-]{1,127}$"))
        {
            error = new SetSecretError.InvalidProperties("Secret name is invalid.");
            return false;
        }

        if (inputSecret.Properties.ContentType?.Length > 255)
        {
            error = new SetSecretError.InvalidProperties("Content type too long.");
            return false;
        }


        if (inputSecret.Properties.Tags.Count > 15)
        {
            error = new SetSecretError.InvalidProperties("Too many tags.");
            return false;
        }

        var invalidTags = inputSecret
            .Properties
            .Tags
            .Where(kv => kv.Key.Length > 512 || kv.Value.Length > 512)
            .Select(kv => $"{kv.Key} = {kv.Value}")
            .ToList();

        if (invalidTags.Count > 0)
        {
            error = new SetSecretError.InvalidProperties($"Invalid tags: {string.Join(", ", invalidTags)}.");
            return false;
        }

        error = null;
        return true;
    }

    public abstract class StartDeleteSecretError
    {
        public abstract RequestFailedException GetClientException();

        internal class SecretNotFound(Uri vaultUri, string secretName) : StartDeleteSecretError
        {
            public override RequestFailedException GetClientException()
            {
                return KeyVaultExceptionFactory.SecretNotFound(vaultUri, secretName);
            }
        }
    }

    public abstract class ListSecretsError
    {
        public abstract RequestFailedException GetClientException();
    }

    public abstract class ListSecretVersionsError
    {
        public abstract RequestFailedException GetClientException();

        public class SecretNotFound(Uri vaultUri, string secretName) : ListSecretVersionsError
        {
            public override RequestFailedException GetClientException()
            {
                return KeyVaultExceptionFactory.SecretNotFound(vaultUri, secretName);
            }
        }
    }

    public abstract class GetSecretError
    {
        public abstract RequestFailedException GetClientException();

        public class SecretNotFound(Uri vaultUri, string secretName) : GetSecretError
        {
            public override RequestFailedException GetClientException()
            {
                return KeyVaultExceptionFactory.SecretNotFound(vaultUri, secretName);
            }
        }

        public class VersionNotFound(Uri vaultUri, string secretName, string secretVersion) : GetSecretError
        {
            public override RequestFailedException GetClientException()
            {
                return KeyVaultExceptionFactory.SecretVersionNotFound(vaultUri, secretName, secretVersion);
            }
        }

        public class SecretDisabled(Uri vaultUri, string secretName) : GetSecretError
        {
            public override RequestFailedException GetClientException()
            {
                return KeyVaultExceptionFactory.SecretDisabled(vaultUri, secretName);
            }
        }
    }

    public abstract class SetSecretError
    {
        public abstract RequestFailedException GetClientException();

        public class InvalidProperties(string detail) : SetSecretError
        {
            public override RequestFailedException GetClientException()
            {
                return new RequestFailedException(400, $"Invalid properties: {detail}");
            }
        }

        public class SecretIsDeleted(Uri vaultUri, string secretName) : SetSecretError
        {
            public override RequestFailedException GetClientException()
            {
                return KeyVaultExceptionFactory.SecretIsDeleted(vaultUri, secretName);
            }
        }
    }

    public abstract class UpdateSecretVersionPropertiesError
    {
        public abstract RequestFailedException GetClientException();

        public class SecretNotFound(Uri vaultUri, string secretName) : UpdateSecretVersionPropertiesError
        {
            public override RequestFailedException GetClientException()
            {
                return KeyVaultExceptionFactory.SecretNotFound(vaultUri, secretName);
            }
        }

        public class VersionNotSpecified(Uri vaultUri, string secretName) : UpdateSecretVersionPropertiesError
        {
            public override RequestFailedException GetClientException()
            {
                return KeyVaultExceptionFactory.VersionNotSpecified(vaultUri, secretName);
            }
        }

        public class VersionNotFound(Uri vaultUri, string secretName, string secretVersion) : UpdateSecretVersionPropertiesError
        {
            public override RequestFailedException GetClientException()
            {
                return KeyVaultExceptionFactory.SecretVersionNotFound(vaultUri, secretName, secretVersion);
            }
        }
    }
}
