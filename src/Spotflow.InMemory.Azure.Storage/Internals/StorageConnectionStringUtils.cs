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
        var props = ParseProperties(connectionString);

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

    public static string GetConnectionString(InMemoryStorageAccount account)
    {
        return $"AccountName={account.Name};AccountKey={account.PrimaryAccessKey};DefaultEndpointsProtocol=https;TableEndpoint={account.TableService.Uri};BlobEndpoint={account.BlobService.Uri}";
    }

    public static bool TryGetSharedKey(string connectionString, [NotNullWhen(true)] out StorageSharedKeyCredential? result)
    {
        var props = ParseProperties(connectionString);

        if (!props.TryGetValue("AccountName", out var accountName))
        {
            result = null;
            return false;
        }

        if (!props.TryGetValue("AccountKey", out var accountKey))
        {
            result = null;
            return false;
        }

        result = new StorageSharedKeyCredential(accountName, accountKey);
        return true;
    }

    private static IReadOnlyDictionary<string, string> ParseProperties(string connectionString)
    {
        var options = StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries;

        var pairs = connectionString.Split(';', options);

        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in pairs)
        {
            KeyValuePair<string, string>? kv = pair.Split('=', options) switch
            {
                [var k, var v] => new(k, v),
                _ => null
            };

            if (kv is not null)
            {
                props.Add(kv.Value.Key, kv.Value.Value);
            }
        }

        return props;
    }
}
