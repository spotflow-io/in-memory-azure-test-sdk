using System.Diagnostics.CodeAnalysis;

using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;

using Spotflow.InMemory.Azure.Storage.Resources;

namespace Spotflow.InMemory.Azure.Storage.Internals;

internal static class StorageConnectionStringUtils
{
    public static string GetAccountNameFromConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var props = ParseProperties(connectionString);

        return GetAccountNameFromConnectionString(props);
    }

    public static string GetConnectionString(InMemoryStorageAccount account)
    {
        return $"AccountName={account.Name};AccountKey={account.PrimaryAccessKey};DefaultEndpointsProtocol=https;TableEndpoint={account.TableService.Uri};BlobEndpoint={account.BlobService.Uri}";
    }

    public static bool TryGetSharedKey(string connectionString, [NotNullWhen(true)] out StorageSharedKeyCredential? result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var props = ParseProperties(connectionString);

        var accountName = GetAccountNameFromConnectionString(props);

        if (!props.TryGetValue("AccountKey", out var accountKey))
        {
            result = null;
            return false;
        }

        result = new StorageSharedKeyCredential(accountName, accountKey);
        return true;
    }

    private static string GetAccountNameFromConnectionString(IReadOnlyDictionary<string, string> props)
    {
        if (props.TryGetValue("AccountName", out var accountName))
        {
            return accountName;
        }

        if (props.TryGetValue("TableEndpoint", out var tableEndpoint))
        {
            return new TableUriBuilder(new Uri(tableEndpoint)).AccountName;
        }

        if (props.TryGetValue("BlobEndpoint", out var blobEndpoint))
        {
            return new BlobUriBuilder(new Uri(blobEndpoint)).AccountName;
        }

        throw new InvalidOperationException("Storage account name could not be resolved.");
    }

    private static IReadOnlyDictionary<string, string> ParseProperties(string connectionString)
    {
        var pairs = connectionString.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=');

            if (keyValue.Length >= 2)
            {
                var key = keyValue[0].Trim();
                var value = string.Join('=', keyValue[1..]).Trim();
                props.Add(key, value);
            }
        }

        return props;
    }
}
