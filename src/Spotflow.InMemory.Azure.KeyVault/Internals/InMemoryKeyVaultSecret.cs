using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

using Azure.Security.KeyVault.Secrets;

namespace Spotflow.InMemory.Azure.KeyVault.Internals;

internal class InMemoryKeyVaultSecret
{
    private readonly ImmutableArray<InMemoryKeyVaultSecretVersion> _versions;
    private readonly Uri _vaultUri;
    private readonly string _secretName;
    private readonly TimeProvider _timeProvider;
    private readonly SecretProperties _properties;

    [MemberNotNullWhen(true, nameof(DeletedOn))]
    public bool IsDeleted { get; }

    public DateTimeOffset? DeletedOn { get; }

    private InMemoryKeyVaultSecret(
        Uri vaultUri,
        string secretName,
        ImmutableArray<InMemoryKeyVaultSecretVersion> versions,
        TimeProvider timeProvider,
        DateTimeOffset? createdOn,
        bool? isDeleted,
        DateTimeOffset? deletedOn,
        bool? isEnabled
        )
    {
        _vaultUri = vaultUri;
        _secretName = secretName;
        _timeProvider = timeProvider;
        _versions = versions;

        var now = _timeProvider.GetUtcNow();

        var id = new Uri(vaultUri, $"/secrets/{secretName}");

        _properties = SecretModelFactory.SecretProperties(
            id: id,
            vaultUri: vaultUri,
            name: secretName,
            createdOn: createdOn ?? now,
            updatedOn: now
            );

        _properties.Enabled = isEnabled;


        IsDeleted = isDeleted ?? false;

        if (IsDeleted && deletedOn is null)
        {
            throw new ArgumentException("Deleted secret must have a deletedOn date", nameof(deletedOn));
        }

        DeletedOn = deletedOn;
    }

    public static InMemoryKeyVaultSecret CreateWithInitialVersion(Uri vaultUri, string secretName, KeyVaultSecret initialVersion, TimeProvider timeProvider)
    {
        var version = InMemoryKeyVaultSecretVersion.FromClientModel(vaultUri, secretName, initialVersion, timeProvider);
        return new(
            vaultUri: vaultUri,
            secretName: secretName,
            versions: [version],
            timeProvider: timeProvider,
            createdOn: null,
            isDeleted: null,
            deletedOn: null,
            isEnabled: initialVersion.Properties.Enabled);
    }


    public InMemoryKeyVaultSecretVersion? FindLatestVersion()
    {
        if (_versions.Length > 0)
        {
            return _versions[^1];
        }

        return null;
    }

    public InMemoryKeyVaultSecretVersion GetLastVersion()
    {
        return FindLatestVersion() ?? throw new InvalidOperationException("No versions found");
    }

    public InMemoryKeyVaultSecretVersion? FindVersion(string secretVersion)
    {
        foreach (var version in _versions)
        {
            if (version.Label.Equals(secretVersion, StringComparison.Ordinal))
            {
                return version;
            }
        }

        return null;
    }

    public InMemoryKeyVaultSecret WithNewVersion(KeyVaultSecret inputSecretVersion)
    {
        var version = InMemoryKeyVaultSecretVersion.FromClientModel(_vaultUri, _secretName, inputSecretVersion, _timeProvider);

        var updatedVersions = _versions.Add(version);

        return Updated(updatedVersions);
    }

    public SecretProperties ToClientListItem() => _properties;

    public InMemoryKeyVaultSecret WithUpdatedVersionProperties(string secretVersion, SecretProperties inputVersionProperties)
    {
        for (var i = 0; i < _versions.Length; i++)
        {
            var version = _versions[i];

            if (version.Label.Equals(secretVersion, StringComparison.Ordinal))
            {
                var updatedVersion = version.WithUpdatedProperties(inputVersionProperties);

                var updatedVersions = _versions.SetItem(i, updatedVersion);

                return Updated(updatedVersions, isEnabled: inputVersionProperties.Enabled);
            }
        }

        throw new InvalidOperationException($"Version '{secretVersion}' not found.");

    }

    public InMemoryKeyVaultSecretVersion GetVersion(string secretVersion)
    {
        return FindVersion(secretVersion) ?? throw new InvalidOperationException("Version not found");
    }

    public bool HasVersion(string secretVersion) => FindVersion(secretVersion) is not null;
    public bool IsEnabled => _properties.Enabled ?? true;

    private InMemoryKeyVaultSecret Updated(
        ImmutableArray<InMemoryKeyVaultSecretVersion>? updatedVersions = null,
        bool? isDeleted = null,
        DateTimeOffset? deletedOn = null,
        bool? isEnabled = null)
    {
        updatedVersions ??= _versions;
        isDeleted ??= IsDeleted;

        return new(
            vaultUri: _vaultUri,
            secretName: _secretName,
            versions: updatedVersions.Value,
            timeProvider: _timeProvider,
            createdOn: _properties.CreatedOn,
            isDeleted: isDeleted.Value,
            deletedOn: deletedOn,
            isEnabled: isEnabled
            );
    }

    public IReadOnlyList<InMemoryKeyVaultSecretVersion> GetVersions() => _versions;

    public InMemoryKeyVaultSecret Deleted() => Updated(isDeleted: true, deletedOn: _timeProvider.GetUtcNow());

    public SecretProperties ToClientProperties() => _properties;

}
