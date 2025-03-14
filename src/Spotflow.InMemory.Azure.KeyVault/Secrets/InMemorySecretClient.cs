using Azure;
using Azure.Security.KeyVault.Secrets;

using Spotflow.InMemory.Azure.Internals;
using Spotflow.InMemory.Azure.KeyVault.Internals;
using Spotflow.InMemory.Azure.KeyVault.Resources;
using Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks;
using Spotflow.InMemory.Azure.KeyVault.Secrets.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.KeyVault.Secrets;

public class InMemorySecretClient(Uri vaultUri, InMemoryKeyVaultProvider provider) : SecretClient
{
    private const int _defaultMaxPageSize = 10;

    public override Uri VaultUri { get; } = vaultUri;
    public string VaultName { get; } = provider.GetVaultNameFromUri(vaultUri);

    public InMemoryKeyVaultProvider Provider => provider;

    public static InMemorySecretClient FromVault(InMemoryKeyVault vault) => new(vault.VaultUri, vault.Provider);

    #region GetSecret

    public override Response<KeyVaultSecret> GetSecret(string name, string? version = null, CancellationToken cancellationToken = default)
    {
        return GetSecretAsync(name, version, cancellationToken).EnsureCompleted();
    }

    public override async Task<Response<KeyVaultSecret>> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return await GetSecretCoreAsync(name, version, cancellationToken);
    }

    private async Task<Response<KeyVaultSecret>> GetSecretCoreAsync(string name, string? version, CancellationToken cancellationToken)
    {
        var scope = new SecretScope(VaultName, name);

        var beforeContext = new GetSecretBeforeHookContext(scope, Provider, cancellationToken)
        {
            RequestedSecretVersion = version
        };

        await ExecuteBeforeHooksAsync(beforeContext);

        var vault = GetSecretsService();

        if (!vault.TryGetSecret(name, version, out var secret, out var error))
        {
            throw error.GetClientException();
        }

        var afterContext = new GetSecretAfterHookContext(beforeContext)
        {
            Secret = secret
        };

        await ExecuteAfterHooksAsync(afterContext);

        return InMemoryResponse.FromValue(secret, 200);
    }

    #endregion

    #region SetSecret

    public override async Task<Response<KeyVaultSecret>> SetSecretAsync(KeyVaultSecret secret, CancellationToken cancellationToken = default)
    {

        return await SetSecretCoreAsync(secret, cancellationToken);
    }

    public override Response<KeyVaultSecret> SetSecret(KeyVaultSecret secret, CancellationToken cancellationToken = default)
    {
        return SetSecretAsync(secret, cancellationToken).EnsureCompleted();
    }

    public override Response<KeyVaultSecret> SetSecret(string name, string value, CancellationToken cancellationToken = default)
    {
        return SetSecretAsync(name, value, cancellationToken).EnsureCompleted();
    }

    public override Task<Response<KeyVaultSecret>> SetSecretAsync(string name, string value, CancellationToken cancellationToken = default)
    {
        return SetSecretAsync(new KeyVaultSecret(name, value), cancellationToken);
    }

    private async Task<Response<KeyVaultSecret>> SetSecretCoreAsync(KeyVaultSecret secret, CancellationToken cancellationToken)
    {
        await Task.Yield();

        var scope = new SecretScope(VaultName, secret.Name);

        var beforeContext = new SetSecretBeforeHookContext(scope, Provider, cancellationToken)
        {
            Secret = secret
        };

        await ExecuteBeforeHooksAsync(beforeContext);

        var vault = GetSecretsService();

        if (!vault.TrySetSecret(secret, out var createdSecret, out var error, cancellationToken))
        {
            throw error.GetClientException();
        }

        var afterContext = new SetSecretAfterHookContext(beforeContext)
        {
            CreatedSecret = createdSecret
        };

        await ExecuteAfterHooksAsync(afterContext);

        return InMemoryResponse.FromValue(createdSecret, 200);
    }

    #endregion

    #region GetPropertiesOfSecrets

    public override Pageable<SecretProperties> GetPropertiesOfSecrets(CancellationToken cancellationToken = default)
    {
        var items = GetPropertiesOfSecretsCore();

        return new InMemoryPageable.Sync<SecretProperties>(items, _defaultMaxPageSize);
    }

    public override AsyncPageable<SecretProperties> GetPropertiesOfSecretsAsync(CancellationToken cancellationToken = default)
    {
        var items = GetPropertiesOfSecretsCore();
        return new InMemoryPageable.YieldingAsync<SecretProperties>(items, _defaultMaxPageSize);
    }

    private IReadOnlyList<SecretProperties> GetPropertiesOfSecretsCore()
    {
        var vault = GetSecretsService();

        if (!vault.TryListSecrets(out var secrets, out var error))
        {
            throw error.GetClientException();
        }

        return secrets;
    }

    #endregion

    #region GetPropertiesOfSecretVersions

    public override AsyncPageable<SecretProperties> GetPropertiesOfSecretVersionsAsync(string name, CancellationToken cancellationToken = default)
    {
        var items = GetPropertiesOfSecretVersionsCoreAsync(name);
        return new InMemoryPageable.YieldingAsync<SecretProperties>(items, _defaultMaxPageSize);
    }

    public override Pageable<SecretProperties> GetPropertiesOfSecretVersions(string name, CancellationToken cancellationToken = default)
    {
        var items = GetPropertiesOfSecretVersionsCoreAsync(name);
        return new InMemoryPageable.Sync<SecretProperties>(items, _defaultMaxPageSize);
    }

    private IReadOnlyList<SecretProperties> GetPropertiesOfSecretVersionsCoreAsync(string name)
    {
        var vault = GetSecretsService();

        if (!vault.TryListSecretVersions(name, out var versions, out var error))
        {
            throw error.GetClientException();
        }

        return versions;
    }

    #endregion

    #region StartDeleteSecret

    public override async Task<DeleteSecretOperation> StartDeleteSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return await StartDeleteSecretCoreAsync(name, cancellationToken);
    }

    public override DeleteSecretOperation StartDeleteSecret(string name, CancellationToken cancellationToken = default)
    {
        return StartDeleteSecretAsync(name, cancellationToken).EnsureCompleted();
    }

    private Task<DeleteSecretOperation> StartDeleteSecretCoreAsync(string name, CancellationToken cancellationToken)
    {
        var vault = GetSecretsService();

        if (!vault.TryStartDeleteSecret(name, out var operation, out var error, cancellationToken))
        {
            throw error.GetClientException();
        }

        return Task.FromResult(operation);
    }

    #endregion

    #region UpdateSecretProperties

    public override async Task<Response<SecretProperties>> UpdateSecretPropertiesAsync(SecretProperties properties, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return await UpdateSecretPropertiesCoreAsync(properties, cancellationToken);
    }

    public override Response<SecretProperties> UpdateSecretProperties(SecretProperties properties, CancellationToken cancellationToken = default)
    {
        return UpdateSecretPropertiesAsync(properties, cancellationToken).EnsureCompleted();
    }

    private Task<Response<SecretProperties>> UpdateSecretPropertiesCoreAsync(SecretProperties properties, CancellationToken cancellationToken)
    {
        var vault = GetSecretsService();

        if (!vault.TryUpdateSecretVersionProperties(properties, out var updatedProperties, out var error, cancellationToken))
        {
            throw error.GetClientException();
        }

        return Task.FromResult(InMemoryResponse.FromValue(updatedProperties, 200));
    }

    #endregion

    private InMemoryKeyVaultSecretsService GetSecretsService()
    {
        if (!provider.TryGetVaultByUri(VaultUri, out var vault))
        {
            throw KeyVaultExceptionFactory.KeyVaultNotFound(VaultUri);
        }

        return vault.Secrets;
    }

    private Task ExecuteBeforeHooksAsync<TContext>(TContext context) where TContext : SecretBeforeHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

    private Task ExecuteAfterHooksAsync<TContext>(TContext context) where TContext : SecretAfterHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

    #region Unsupported

    public override Task<Response<DeletedSecret>> GetDeletedSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        throw KeyVaultExceptionFactory.MethodNotSupported();
    }

    public override Response<DeletedSecret> GetDeletedSecret(string name, CancellationToken cancellationToken = default)
    {
        throw KeyVaultExceptionFactory.MethodNotSupported();
    }

    public override AsyncPageable<DeletedSecret> GetDeletedSecretsAsync(CancellationToken cancellationToken = default)
    {
        throw KeyVaultExceptionFactory.MethodNotSupported();
    }

    public override Pageable<DeletedSecret> GetDeletedSecrets(CancellationToken cancellationToken = default)
    {
        throw KeyVaultExceptionFactory.MethodNotSupported();
    }

    public override Task<RecoverDeletedSecretOperation> StartRecoverDeletedSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        throw KeyVaultExceptionFactory.MethodNotSupported();
    }

    public override RecoverDeletedSecretOperation StartRecoverDeletedSecret(string name, CancellationToken cancellationToken = default)
    {
        throw KeyVaultExceptionFactory.MethodNotSupported();
    }

    public override Task<Response> PurgeDeletedSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        throw KeyVaultExceptionFactory.MethodNotSupported();
    }

    public override Response PurgeDeletedSecret(string name, CancellationToken cancellationToken = default)
    {
        throw KeyVaultExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<byte[]>> BackupSecretAsync(string name, CancellationToken cancellationToken = default)
    {
        throw KeyVaultExceptionFactory.MethodNotSupported();
    }

    public override Response<byte[]> BackupSecret(string name, CancellationToken cancellationToken = default)
    {
        throw KeyVaultExceptionFactory.MethodNotSupported();
    }

    public override Task<Response<SecretProperties>> RestoreSecretBackupAsync(byte[] backup, CancellationToken cancellationToken = default)
    {
        throw KeyVaultExceptionFactory.MethodNotSupported();
    }

    public override Response<SecretProperties> RestoreSecretBackup(byte[] backup, CancellationToken cancellationToken = default)
    {
        throw KeyVaultExceptionFactory.MethodNotSupported();
    }

    #endregion
}
