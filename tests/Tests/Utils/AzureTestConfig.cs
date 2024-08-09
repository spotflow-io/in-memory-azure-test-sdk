using System.Diagnostics.CodeAnalysis;

using Azure.Core;
using Azure.Identity;

namespace Tests.Utils;

internal static class AzureTestConfig
{
    public class Values
    {
        public required string TenantId { get; init; }
        public required string SubscriptionId { get; init; }
        public required string ResourceGroupName { get; init; }
        public required string StorageAccountName { get; init; }
        public required Uri BlobServiceUri { get; init; }
        public required Uri TableServiceUri { get; init; }
        public required string ServiceBusNamespaceName { get; init; }
        public required string KeyVaultName { get; init; }
        public required Uri KeyVaultUri { get; init; }
        public required TokenCredential TokenCredential { get; init; }
    }

    private static readonly Values? _values;

    static AzureTestConfig()
    {
        if (!UseAzure())
        {
            return;
        }

        var tenantId = GetRequiredString("AZURE_TENANT_ID");

        var storageAccountName = GetRequiredString("AZURE_STORAGE_ACCOUNT_NAME");
        var keyVaultName = GetRequiredString("AZURE_KEY_VAULT_NAME");

        _values = new()
        {
            TenantId = tenantId,
            SubscriptionId = GetRequiredString("AZURE_SUBSCRIPTION_ID"),
            ResourceGroupName = GetRequiredString("AZURE_RESOURCE_GROUP_NAME"),
            StorageAccountName = storageAccountName,
            BlobServiceUri = new($"https://{storageAccountName}.blob.core.windows.net/"),
            TableServiceUri = new($"https://{storageAccountName}.table.core.windows.net/"),
            ServiceBusNamespaceName = GetRequiredString("AZURE_SERVICE_BUS_NAMESPACE_NAME"),
            KeyVaultName = keyVaultName,
            KeyVaultUri = new($"https://{keyVaultName}.vault.azure.net/"),
            TokenCredential = new AzureCliCredential(options: new() { TenantId = tenantId })
        };
    }

    public static bool IsAvailable([NotNullWhen(true)] out Values? result) => (result = _values) is not null;

    private static bool UseAzure()
    {
        var flag = Environment.GetEnvironmentVariable("SPOTFLOW_USE_AZURE") ?? "false";
        return flag.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRequiredString(string environmentVariableName)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariableName);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Environment variable '{environmentVariableName}' is must be set when using Azure.");
        }

        return value;
    }
}


