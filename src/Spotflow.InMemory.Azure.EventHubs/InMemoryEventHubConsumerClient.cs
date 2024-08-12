using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;

using Spotflow.InMemory.Azure.EventHubs.Hooks;
using Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;
using Spotflow.InMemory.Azure.EventHubs.Internals;
using Spotflow.InMemory.Azure.EventHubs.Resources;

namespace Spotflow.InMemory.Azure.EventHubs;

public class InMemoryEventHubConsumerClient : EventHubConsumerClient
{
    private readonly ConsumerEventHubScope _scope;
    #region Constructors

    public InMemoryEventHubConsumerClient(
        string consumerGroup,
        string connectionString,
        InMemoryEventHubProvider provider)
        : this(consumerGroup, EventHubClientUtils.ConnectionFromConnectionString(connectionString), provider) { }

    public InMemoryEventHubConsumerClient(
        string consumerGroup,
        string fullyQualifiedNamespace,
        string eventHubName,
        InMemoryEventHubProvider provider)
        : this(consumerGroup, EventHubClientUtils.Connection(fullyQualifiedNamespace, eventHubName), provider) { }


    public InMemoryEventHubConsumerClient(
        string consumerGroup,
        EventHubConnection connection,
        InMemoryEventHubProvider provider)
        : base(consumerGroup, connection)
    {
        Provider = provider;
        var namespaceName = Provider.GetNamespaceNameFromHostname(FullyQualifiedNamespace);
        _scope = new ConsumerEventHubScope(namespaceName, EventHubName, ConsumerGroup);
    }

    public static InMemoryEventHubConsumerClient FromEventHub(string consumerGroup, InMemoryEventHub eventHub)
    {
        return FromNamespace(consumerGroup, eventHub.Namespace, eventHub.Name);
    }

    public static InMemoryEventHubConsumerClient FromEventHub(InMemoryEventHub eventHub)
    {
        return FromEventHub(DefaultConsumerGroupName, eventHub);
    }

    public static InMemoryEventHubConsumerClient FromNamespace(string consumerGroup, InMemoryEventHubNamespace eventHubNamespace, string eventHubName)
    {
        return new(consumerGroup, eventHubNamespace.FullyQualifiedNamespace, eventHubName, eventHubNamespace.Provider);
    }

    #endregion

    public InMemoryEventHubProvider Provider { get; }

    #region Get Properties & IDs

    public override async Task<EventHubProperties> GetEventHubPropertiesAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var beforeContext = new GetEventHubPropertiesConsumerBeforeHookContext(_scope, Provider, cancellationToken);

        await ExecuteBeforeHooksAsync(beforeContext);

        var eventHub = GetEventHub();

        var properties = eventHub.Properties;

        var afterContext = new GetEventHubPropertiesConsumerAfterHookContext(beforeContext)
        {
            EventHubProperties = properties
        };

        await ExecuteAfterHooksAsync(afterContext);

        return properties;
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

        var scope = _scope.WithPartition(partitionId);

        var beforeContext = new GetPartitionPropertiesConsumerBeforeHookContext(scope, Provider, cancellationToken);

        await ExecuteBeforeHooksAsync(beforeContext);

        var eventHub = GetEventHub();

        var properties = eventHub.GetPartition(partitionId).GetProperties();

        var afterContext = new GetPartitionPropertiesConsumerAfterHookContext(beforeContext)
        {
            PartitionProperties = properties
        };

        await ExecuteAfterHooksAsync(afterContext);

        return properties;
    }

    #endregion

    #region Dispose & Close 

    public override async ValueTask DisposeAsync()
    {
        await Task.Yield();
        IsClosed = true;
    }

    public override async Task CloseAsync(CancellationToken cancellationToken = default) => await DisposeAsync();

    #endregion

    private InMemoryEventHub GetEventHub()
    {
        var eventHub = EventHubClientUtils.GetEventHub(Provider, FullyQualifiedNamespace, EventHubName);

        EventHubClientUtils.HasConsumerGroupOrThrow(eventHub, ConsumerGroup);

        return eventHub;
    }

    private Task ExecuteBeforeHooksAsync<TContext>(TContext context) where TContext : ConsumerBeforeHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

    private Task ExecuteAfterHooksAsync<TContext>(TContext context) where TContext : ConsumerAfterHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }


    #region Unsupported

    public override IAsyncEnumerable<PartitionEvent> ReadEventsFromPartitionAsync(string partitionId, EventPosition startingPosition, CancellationToken cancellationToken = default)
    {
        throw EventHubExceptionFactory.MethodNotSupported();
    }

    public override IAsyncEnumerable<PartitionEvent> ReadEventsFromPartitionAsync(string partitionId, EventPosition startingPosition, ReadEventOptions readOptions, CancellationToken cancellationToken = default)
    {
        throw EventHubExceptionFactory.MethodNotSupported();
    }

    public override IAsyncEnumerable<PartitionEvent> ReadEventsAsync(CancellationToken cancellationToken = default)
    {
        throw EventHubExceptionFactory.MethodNotSupported();
    }

    public override IAsyncEnumerable<PartitionEvent> ReadEventsAsync(ReadEventOptions readOptions, CancellationToken cancellationToken = default)
    {
        throw EventHubExceptionFactory.MethodNotSupported();
    }

    public override IAsyncEnumerable<PartitionEvent> ReadEventsAsync(bool startReadingAtEarliestEvent, ReadEventOptions? readOptions = null, CancellationToken cancellationToken = default)
    {
        throw EventHubExceptionFactory.MethodNotSupported();
    }

    #endregion

}
