using Azure.Security.KeyVault.Secrets;

namespace Spotflow.InMemory.Azure.KeyVault.Internals;

internal class InMemoryKeyVaultSecretVersion(Uri vaultUri, string secretName, string versionId, string value, SecretProperties properties, TimeProvider timeProvider)
{
    public string Label => versionId;

    public static InMemoryKeyVaultSecretVersion FromClientModel(Uri vaultUri, string secretName, KeyVaultSecret inputSecretVersion, TimeProvider timeProvider)
    {
        var version = GenerateSecretVersionLabel();

        var id = new Uri(vaultUri, $"/secrets/{secretName}/{version}");

        var now = timeProvider.GetUtcNow();

        var properties = SecretModelFactory.SecretProperties(
            id: id,
            vaultUri: vaultUri,
            name: secretName,
            version: version,
            createdOn: now,
            updatedOn: now);

        SetAttributesAndTags(inputSecretVersion.Properties, properties);

        return new(vaultUri, secretName, version, inputSecretVersion.Value, properties, timeProvider);
    }

    public InMemoryKeyVaultSecretVersion WithUpdatedProperties(SecretProperties inputProperties)
    {
        var now = timeProvider.GetUtcNow();

        var updatedProperties = SecretModelFactory.SecretProperties(
            id: properties.Id,
            vaultUri: properties.VaultUri,
            name: properties.Name,
            version: properties.Version,
            createdOn: properties.CreatedOn,
            updatedOn: now);

        SetAttributesAndTags(inputProperties, updatedProperties);

        return new(vaultUri, secretName, versionId, value, updatedProperties, timeProvider);
    }

    public KeyVaultSecret ToClientModel() => SecretModelFactory.KeyVaultSecret(properties, value);

    private static string GenerateSecretVersionLabel() => Guid.NewGuid().ToString("N");

    private static void SetAttributesAndTags(SecretProperties source, SecretProperties target)
    {
        target.NotBefore = source.NotBefore;
        target.ExpiresOn = source.ExpiresOn;
        target.ContentType = source.ContentType;
        target.Enabled = source.Enabled;

        foreach (var (k, v) in source.Tags)
        {
            target.Tags[k] = v;
        }
    }
}
