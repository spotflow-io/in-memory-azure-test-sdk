using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Azure.Core;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

using Spotflow.InMemory.Azure.Auth;
using Spotflow.InMemory.Azure.EventHubs.Hooks;
using Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;
using Spotflow.InMemory.Azure.EventHubs.Internals;
using Spotflow.InMemory.Azure.EventHubs.Resources;

namespace Spotflow.InMemory.Azure.EventHubs;

public class InMemoryEventHubProducerClient : EventHubProducerClient
{
    private readonly ConcurrentDictionary<EventDataBatch, (List<EventData> Events, SendEventOptions Options)> _batches
        = new(ReferenceEqualityComparer.Instance);

    private readonly EventHubScope _scope;

    #region Constructors

    public InMemoryEventHubProducerClient(string connectionString, InMemoryEventHubProvider provider)
        : this(EventHubClientUtils.ConnectionFromConnectionString(connectionString), provider) { }

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Distinguishing from other constructors")]
    public InMemoryEventHubProducerClient(string fullyQualifiedNamespace, string eventHubName, TokenCredential tokenCredential, InMemoryEventHubProvider provider)
        : this(EventHubClientUtils.Connection(fullyQualifiedNamespace, eventHubName), provider) { }


    public InMemoryEventHubProducerClient(EventHubConnection connection, InMemoryEventHubProvider provider) : base(connection)
    {
        Provider = provider;
        _scope = new(provider.GetNamespaceNameFromHostname(FullyQualifiedNamespace), EventHubName);
    }

    public static InMemoryEventHubProducerClient FromEventHub(InMemoryEventHub eventHub)
    {
        return FromNamespace(eventHub.Namespace, eventHub.Name);
    }

    public static InMemoryEventHubProducerClient FromNamespace(InMemoryEventHubNamespace eventHubNamespace, string eventHubName)
    {
        return new(eventHubNamespace.FullyQualifiedNamespace, eventHubName, NoOpTokenCredential.Instance, eventHubNamespace.Provider);
    }

    #endregion

    public InMemoryEventHubProvider Provider { get; }

    #region Dispose & Close

    public override async ValueTask DisposeAsync()
    {
        await Task.Yield();
        IsClosed = true;
    }
    public override async Task CloseAsync(CancellationToken cancellationToken = default) => await DisposeAsync();

    #endregion

    #region Create Batch

    public override ValueTask<EventDataBatch> CreateBatchAsync(CancellationToken cancellationToken = default)
    {
        return CreateBatchAsync(new CreateBatchOptions(), cancellationToken);
    }

    public override async ValueTask<EventDataBatch> CreateBatchAsync(CreateBatchOptions options, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var events = new List<EventData>();

        var batch = EventHubsModelFactory.EventDataBatch(42, events, options, ed => events.Count < 64);

        _batches[batch] = (events, options);

        return batch;
    }

    #endregion

    #region  Get properties & IDs

    public override async Task<EventHubProperties> GetEventHubPropertiesAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var eventHub = GetEventHub();

        return eventHub.Properties;
    }

    public override async Task<string[]> GetPartitionIdsAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var eventHub = GetEventHub();

        return eventHub.Properties.PartitionIds;
    }

    public override async Task<PartitionProperties> GetPartitionPropertiesAsync(string partitionId, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var eventHub = GetEventHub();

        return eventHub.GetPartition(partitionId).GetProperties();

    }

    #endregion

    #region Send

    public override async Task SendAsync(IEnumerable<EventData> eventBatch, SendEventOptions sendEventOptions, CancellationToken cancellationToken = default)
    {
        await SendCoreAsync(eventBatch, sendEventOptions, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    }

    public override async Task SendAsync(IEnumerable<EventData> eventBatch, CancellationToken cancellationToken = default)
    {
        await SendCoreAsync(eventBatch, null, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    }

    public override async Task SendAsync(EventDataBatch eventBatch, CancellationToken cancellationToken = default)
    {
        if (!_batches.TryRemove(eventBatch, out var data))
        {
            throw EventHubExceptionFactory.FeatureNotSupported($"Batches from different instance of '{GetType()}' cannot be sent.");
        }

        var (events, options) = data;

        await SendCoreAsync(events, options, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
    }

    private async Task SendCoreAsync(IEnumerable<EventData> eventBatch, SendEventOptions? sendEventOptions, CancellationToken cancellationToken)
    {
        var enumeratedBatch = eventBatch.ToList();

        var scope = new ProducerEventHubScope(_scope, sendEventOptions);

        var beforeContext = new SendBeforeHookContext(scope, Provider, cancellationToken)
        {
            EventBatch = enumeratedBatch,
            SendEventOptions = sendEventOptions,
        };

        await ExecuteBeforeHooksAsync(beforeContext).ConfigureAwait(ConfigureAwaitOptions.None);

        var partition = ResolvePartitionToSend(sendEventOptions);

        foreach (var e in enumeratedBatch)
        {
            partition.SendEvent(e, sendEventOptions?.PartitionKey);
        }

        var afterContext = new SendAfterHookContext(beforeContext)
        {
            EventBatch = enumeratedBatch,
            PartitionId = partition.PartitionId
        };

        await ExecuteAfterHooksAsync(afterContext).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    private InMemoryPartition ResolvePartitionToSend(SendEventOptions? sendEventOptions)
    {
        var eventHub = GetEventHub();

        if (sendEventOptions?.PartitionId is not null)
        {
            if (!eventHub.TryGetPartition(sendEventOptions.PartitionId, out var partition))
            {
                throw EventHubExceptionFactory.PartitionNotFound(eventHub, sendEventOptions.PartitionId);
            }

            return partition;
        }

        if (sendEventOptions?.PartitionKey is not null)
        {
            return eventHub.GetPartitionByKey(sendEventOptions.PartitionKey);
        }

        return eventHub.GetRoundRobinPartition();
    }

    #endregion

    private InMemoryEventHub GetEventHub()
    {
        return EventHubClientUtils.GetEventHub(Provider, FullyQualifiedNamespace, EventHubName);
    }

    private Task ExecuteBeforeHooksAsync<TContext>(TContext context) where TContext : ProducerBeforeHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

    private Task ExecuteAfterHooksAsync<TContext>(TContext context) where TContext : ProducerAfterHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }


    #region Unsupported

    protected override Task<PartitionPublishingProperties> GetPartitionPublishingPropertiesAsync(string partitionId, CancellationToken cancellationToken = default)
    {
        throw EventHubExceptionFactory.MethodNotSupported();
    }

    #endregion

}
