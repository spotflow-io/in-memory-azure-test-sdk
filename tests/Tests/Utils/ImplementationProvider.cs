using System.Diagnostics.CodeAnalysis;

using Azure.Data.Tables;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Primitives;
using Azure.Messaging.EventHubs.Producer;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;

using Spotflow.InMemory.Azure.Auth;
using Spotflow.InMemory.Azure.EventHubs;
using Spotflow.InMemory.Azure.EventHubs.Resources;
using Spotflow.InMemory.Azure.KeyVault;
using Spotflow.InMemory.Azure.KeyVault.Secrets;
using Spotflow.InMemory.Azure.ServiceBus;
using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;
using Spotflow.InMemory.Azure.Storage.Tables;

namespace Tests.Utils;

internal static class ImplementationProvider
{
    public static bool IsAzureConfigAvailable => AzureTestConfig.IsAvailable(out _);

    private static readonly AzureResourceProvider? _resourceProvider;

    static ImplementationProvider()
    {
        if (AzureTestConfig.IsAvailable(out var config))
        {
            _resourceProvider = new(config);
        }
    }

    public static BlobContainerClient GetBlobContainerClient(string? containerName = null)
    {
        containerName ??= "test";

        if (TryWithAzure(out var config))
        {
            var serviceClient = new BlobServiceClient(config.BlobServiceUri, config.TokenCredential);
            return serviceClient.GetBlobContainerClient(containerName);
        }
        else
        {
            var account = new InMemoryStorageProvider().AddAccount();
            return InMemoryBlobContainerClient.FromAccount(account, containerName);
        }
    }

    public static BlobServiceClient GetBlobServiceClient()
    {
        if (TryWithAzure(out var config))
        {
            return new BlobServiceClient(config.BlobServiceUri, config.TokenCredential);
        }
        else
        {
            var account = new InMemoryStorageProvider().AddAccount();
            return InMemoryBlobServiceClient.FromAccount(account);
        }
    }

    public static TableClient GetTableClient(string? tableName = null)
    {
        tableName ??= "test";

        if (TryWithAzure(out var config))
        {
            var serviceClient = new TableServiceClient(config.TableServiceUri, config.TokenCredential);
            return serviceClient.GetTableClient(tableName);
        }
        else
        {
            var account = new InMemoryStorageProvider().AddAccount();
            return InMemoryTableClient.FromAccount(account, tableName);
        }
    }

    public static async Task<string> GetStorageConnectionString()
    {
        if (!TryWithAzure(out _, out var provider))
        {
            throw new InvalidOperationException("Azure configuration is not available.");
        }

        var storageAccount = await provider.GetStorageAccountAsync();

        await foreach (var key in storageAccount.GetKeysAsync())
        {
            return $"AccountName={storageAccount.Data.Name};AccountKey={key.Value}";
        }

        throw new InvalidOperationException("No storage account keys found.");
    }

    public static async Task<ServiceBusClient> GetServiceBusClientAsync(ServiceBusClientOptions? clientOptions = null)
    {
        if (TryWithAzure(out var config, out var provider))
        {
            var serviceBusResources = await provider.GetServiceBusResources();
            return new ServiceBusClient(serviceBusResources.FullyQualifiedNamespaceName, config.TokenCredential, clientOptions);
        }
        else
        {
            var ns = new InMemoryServiceBusProvider().AddNamespace();
            ns.AddQueue("test-queue-with-sessions-empty", new() { EnableSessions = true });
            ns.AddQueue("test-queue-with-sessions", new() { EnableSessions = true });

            return new InMemoryServiceBusClient(ns.FullyQualifiedNamespace, NoOpTokenCredential.Instance, clientOptions ?? new(), ns.Provider);
        }
    }

    public static async Task<ServiceBusSender> GetServiceBusSenderAsync(bool withSessions = false, bool missingEntity = false, bool useTopics = false, bool missingNamespace = false)
    {
        if (TryWithAzure(out var config, out var provider))
        {
            if (missingNamespace)
            {
                var serviceClient = new ServiceBusClient($"{Guid.NewGuid()}.servicebus.windows.net", config.TokenCredential);
                return serviceClient.CreateSender("test-queue");
            }
            else
            {
                var serviceBusResources = await provider.GetServiceBusResources();

                var serviceClient = new ServiceBusClient(serviceBusResources.FullyQualifiedNamespaceName, config.TokenCredential);

                var entityName = missingEntity ? Guid.NewGuid().ToString() : serviceBusResources.GetEntity(withSessions, useTopics).Id.Name;

                return serviceClient.CreateSender(entityName);
            }
        }
        else
        {
            if (missingNamespace)
            {
                var inMemoryProvider = new InMemoryServiceBusProvider();
                var serviceClient = new InMemoryServiceBusClient("non-existing.servicebus.in-memory.example.com", NoOpTokenCredential.Instance, inMemoryProvider);
                return serviceClient.CreateSender("test-entity");
            }
            else
            {
                var ns = new InMemoryServiceBusProvider().AddNamespace();

                var entityName = Guid.NewGuid().ToString();

                if (!missingEntity)
                {
                    if (useTopics)
                    {
                        ns.AddTopic(entityName).AddSubscription("test-subscription", options: new() { EnableSessions = withSessions });
                    }
                    else
                    {
                        ns.AddQueue(entityName, options: new() { EnableSessions = withSessions });
                    }
                }

                var client = new InMemoryServiceBusClient(ns.FullyQualifiedNamespace, NoOpTokenCredential.Instance, ns.Provider);

                return client.CreateSender(entityName);
            }
        }
    }



    public static SecretClient GetSecretClient()
    {
        if (TryWithAzure(out var config))
        {
            return new SecretClient(config.KeyVaultUri, config.TokenCredential);
        }
        else
        {
            var vault = new InMemoryKeyVaultProvider().AddVault();
            return InMemorySecretClient.FromVault(vault);
        }
    }
    private static bool TryWithAzure([NotNullWhen(true)] out AzureTestConfig.Values? config) => TryWithAzure(out config, out _);

    private static bool TryWithAzure([NotNullWhen(true)] out AzureTestConfig.Values? config, [NotNullWhen(true)] out AzureResourceProvider? provider)
    {
        if (_resourceProvider is null)
        {
            config = null;
            provider = null;
            return false;
        }

        _resourceProvider.InitializeAsync().Wait();

        config = _resourceProvider.Config;
        provider = _resourceProvider;
        return true;
    }

    public static async Task<EventHubProducerClient> GetEventHubProducerClientAsync(InMemoryEventHub inMemoryEventHub)
    {
        if (TryWithAzure(out var config, out var azure))
        {
            var eventHubResources = await azure.GetEventHubResourcesAsync();

            return new EventHubProducerClient(
                eventHubResources.Namespace.Data.ServiceBusEndpoint,
                inMemoryEventHub.Name,
                config.TokenCredential);

        }
        else
        {
            return InMemoryEventHubProducerClient.FromEventHub(inMemoryEventHub);
        }
    }

    public static async Task<EventHubConsumerClient> GetEventHubConsumerClientAsync(InMemoryEventHub inMemoryEventHub)
    {
        if (TryWithAzure(out var config, out var azure))
        {
            var eventHubResources = await azure.GetEventHubResourcesAsync();

            return new EventHubConsumerClient(
                "$default",
                eventHubResources.Namespace.Data.ServiceBusEndpoint,
                inMemoryEventHub.Name,
                config.TokenCredential);

        }
        else
        {
            return InMemoryEventHubConsumerClient.FromEventHub(inMemoryEventHub);
        }
    }

    public static async Task<PartitionReceiver> GetEventHubPartitionReceiverAsync(string partitionId, EventPosition startingPosition, InMemoryEventHub inMemoryEventHub)
    {
        if (TryWithAzure(out var config, out var azure))
        {
            var eventHubResources = await azure.GetEventHubResourcesAsync();

            return new PartitionReceiver(
                "$default",
                partitionId,
                startingPosition,
                eventHubResources.Namespace.Data.ServiceBusEndpoint,
                inMemoryEventHub.Name,
                config.TokenCredential);
        }
        else
        {
            return InMemoryPartitionReceiver.FromEventHub(partitionId, startingPosition, inMemoryEventHub);
        }
    }
}
