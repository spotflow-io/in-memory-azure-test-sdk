using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.ServiceBus;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;

namespace Tests.Utils;

internal class AzureResourceProvider
{
    private readonly Lazy<Task<ResourceGroupResource>> _resourceGroup;
    private readonly Lazy<Task<ServiceBusResources>> _serviceBusResources;
    private readonly Lazy<Task<StorageAccountResource>> _storageAccountResource;
    private readonly Lazy<Task<KeyVaultResource>> _keyVaultResource;

    public AzureResourceProvider(AzureTestConfig.Values config)
    {
        Config = config;

        _resourceGroup = new(PrepareResourceGroupAsync);
        _serviceBusResources = new(PrepareServiceBusResourcesAsync);
        _storageAccountResource = new(PrepareStorageAccountResourceAsync);
        _keyVaultResource = new(PrepareKeyVaultResourceAsync);
    }

    public AzureTestConfig.Values Config { get; }

    public async Task InitializeAsync()
    {
        var tasks = new Task[]
        {
            _resourceGroup.Value,
            _storageAccountResource.Value,
            _serviceBusResources.Value,
            _keyVaultResource.Value
        };

        await Task.WhenAll(tasks);
    }

    public Task<StorageAccountResource> GetStorageAccountAsync() => _storageAccountResource.Value;
    public Task<ServiceBusResources> GetServiceBusResources() => _serviceBusResources.Value;

    private async Task<ResourceGroupResource> PrepareResourceGroupAsync()
    {
        var armClient = new ArmClient(Config.TokenCredential);
        var subscription = armClient.GetSubscriptionResource(ResourceIdentifier.Parse($"/subscriptions/{Config.SubscriptionId}"));
        var resourceGroup = await subscription.GetResourceGroupAsync(Config.ResourceGroupName);
        return resourceGroup.Value;
    }

    private async Task<StorageAccountResource> PrepareStorageAccountResourceAsync()
    {
        var resourceGroup = await _resourceGroup.Value;

        var sku = new StorageSku(StorageSkuName.StandardLrs);

        var storageData = new StorageAccountCreateOrUpdateContent(sku, StorageKind.StorageV2, resourceGroup.Data.Location)
        {
            AccessTier = StorageAccountAccessTier.Hot,
            EnableHttpsTrafficOnly = true,
            AllowBlobPublicAccess = false,
            IsHnsEnabled = false,
            AllowSharedKeyAccess = true,
        };

        var storageAccount = await resourceGroup
            .GetStorageAccounts()
            .CreateOrUpdateAsync(WaitUntil.Completed, Config.StorageAccountName, storageData);

        var managementPolicyRule = new ManagementPolicyRule(
            "cleanup",
            ManagementPolicyRuleType.Lifecycle,
            new(new() { BaseBlob = new() { Delete = new() { DaysAfterCreationGreaterThan = 14 } } }) { Filters = new(["blockBlob"]) });

        var managementPolicyData = new StorageAccountManagementPolicyData
        {
            Rules = [managementPolicyRule]
        };

        await storageAccount.Value
            .GetStorageAccountManagementPolicy()
            .CreateOrUpdateAsync(WaitUntil.Completed, managementPolicyData);

        return storageAccount.Value;
    }

    private async Task<ServiceBusResources> PrepareServiceBusResourcesAsync()
    {
        var resourceGroup = await _resourceGroup.Value;

        var serviceBusNamespace = await resourceGroup
            .GetServiceBusNamespaces()
            .CreateOrUpdateAsync(WaitUntil.Completed, Config.ServiceBusNamespaceName, data: new(resourceGroup.Data.Location));

        var serviceBusNamespaceFqn = serviceBusNamespace.Value.Data.ServiceBusEndpoint;

        var queues = serviceBusNamespace.Value.GetServiceBusQueues();
        var topics = serviceBusNamespace.Value.GetServiceBusTopics();

        var ttl = TimeSpan.FromMinutes(30);

        var queueWithSessionsTask = queues.CreateOrUpdateAsync(WaitUntil.Completed, "test-queue-with-sessions", data: new() { RequiresSession = true, DefaultMessageTimeToLive = ttl });
        var queueWithoutSessionsTask = queues.CreateOrUpdateAsync(WaitUntil.Completed, "test-queue-without-sessions", data: new() { RequiresSession = false, DefaultMessageTimeToLive = ttl });

        var topicTask = topics.CreateOrUpdateAsync(WaitUntil.Completed, "test-topic", data: new() { });

        var topic = await topicTask;

        var subscriptions = topic.Value.GetServiceBusSubscriptions();

        var subWithSessionsTask = subscriptions.CreateOrUpdateAsync(WaitUntil.Completed, "test-subscription-with-sessions", data: new() { RequiresSession = true, DefaultMessageTimeToLive = ttl });
        var subWithoutSessionsTask = subscriptions.CreateOrUpdateAsync(WaitUntil.Completed, "test-subscription-without-sessions", data: new() { RequiresSession = false, DefaultMessageTimeToLive = ttl });

        return new()
        {
            FullyQualifiedNamespaceName = serviceBusNamespaceFqn,
            QueueWithSessions = (await queueWithSessionsTask).Value,
            QueueWithoutSessions = (await queueWithoutSessionsTask).Value,
            SubscriptionWithSessions = (await subWithSessionsTask).Value,
            SubscriptionWithoutSessions = (await subWithoutSessionsTask).Value
        };
    }

    private async Task<KeyVaultResource> PrepareKeyVaultResourceAsync()
    {
        var resourceGroup = await _resourceGroup.Value;

        var content = new KeyVaultCreateOrUpdateContent(
            resourceGroup.Data.Location,
            new(Guid.Parse(Config.TenantId), new KeyVaultSku(KeyVaultSkuFamily.A, KeyVaultSkuName.Standard)) { EnableRbacAuthorization = true });

        var keyVault = await resourceGroup
            .GetKeyVaults()
            .CreateOrUpdateAsync(WaitUntil.Completed, Config.KeyVaultName, content);

        return keyVault.Value;
    }


    public class ServiceBusResources
    {
        public required string FullyQualifiedNamespaceName { get; init; }
        public required ServiceBusQueueResource QueueWithSessions { get; init; }
        public required ServiceBusQueueResource QueueWithoutSessions { get; init; }
        public required ServiceBusSubscriptionResource SubscriptionWithSessions { get; init; }
        public required ServiceBusSubscriptionResource SubscriptionWithoutSessions { get; init; }

        public ArmResource GetEntity(bool withSessions, bool useTopics)
        {
            return (withSessions, useTopics) switch
            {
                (true, true) => SubscriptionWithSessions,
                (true, false) => QueueWithSessions,
                (false, true) => SubscriptionWithoutSessions,
                (false, false) => QueueWithoutSessions,
            };
        }
    }
}
