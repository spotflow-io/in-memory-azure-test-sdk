using System.Diagnostics.CodeAnalysis;

using Azure.Core;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Primitives;

using Spotflow.InMemory.Azure.Auth;
using Spotflow.InMemory.Azure.EventHubs.Hooks;
using Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;
using Spotflow.InMemory.Azure.EventHubs.Internals;
using Spotflow.InMemory.Azure.EventHubs.Resources;

namespace Spotflow.InMemory.Azure.EventHubs;

public class InMemoryPartitionReceiver : PartitionReceiver
{
    private readonly SemaphoreSlim _receiveLock = new(1, 1);
    private readonly object _lastEnqueuedEventPropertiesLock = new();

    private readonly InMemoryEventPosition _startingPosition;
    private readonly TimeProvider _timeProvider;

    private InMemoryEventPosition? _position;

    private LastEnqueuedEventProperties? _lastEnqueuedEventProperties;

    private readonly PartitionConsumerEventHubScope _scope;

    #region Constructors

    [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Distinguishing from other constructors")]
    public InMemoryPartitionReceiver(
        string consumerGroup,
        string partitionId,
        EventPosition startingPosition,
        string fullyQualifiedNamespace,
        string eventHubName,
        TokenCredential tokenCredential,
        InMemoryEventHubProvider provider)
        : this(consumerGroup, partitionId, startingPosition, EventHubClientUtils.Connection(fullyQualifiedNamespace, eventHubName), provider) { }

    public InMemoryPartitionReceiver(
        string consumerGroup,
        string partitionId,
        EventPosition startingPosition,
        string connectionString,
        InMemoryEventHubProvider provider)
        : this(consumerGroup, partitionId, startingPosition, EventHubClientUtils.ConnectionFromConnectionString(connectionString), provider) { }

    public InMemoryPartitionReceiver(
        string consumerGroup,
        string partitionId,
        EventPosition startingPosition,
        string connectionString,
        string eventHubName,
        InMemoryEventHubProvider provider)
    : this(consumerGroup, partitionId, startingPosition, EventHubClientUtils.ConnectionFromConnectionString(connectionString, eventHubName), provider) { }


    public InMemoryPartitionReceiver(
        string consumerGroup,
        string partitionId,
        EventPosition startingPosition,
        EventHubConnection connection,
        InMemoryEventHubProvider provider)
        : base(consumerGroup, partitionId, startingPosition, connection)
    {
        Provider = provider;
        _timeProvider = provider.TimeProvider;
        _startingPosition = InMemoryEventPosition.FromEventPosition(startingPosition);
        _scope = new(provider.GetNamespaceNameFromHostname(FullyQualifiedNamespace), EventHubName, ConsumerGroup, PartitionId);
    }

    public static InMemoryPartitionReceiver FromEventHub(string partitionId, EventPosition startingPosition, InMemoryEventHub eventHub)
    {
        return FromEventHub(InMemoryEventHub.DefaultConsumerGroupName, partitionId, startingPosition, eventHub);
    }

    public static InMemoryPartitionReceiver FromEventHub(string consumerGroup, string partitionId, EventPosition startingPosition, InMemoryEventHub eventHub)
    {
        return FromNamespace(consumerGroup, partitionId, startingPosition, eventHub.Namespace, eventHub.Name);
    }

    public static InMemoryPartitionReceiver FromNamespace(string consumerGroup, string partitionId, EventPosition startingPosition, InMemoryEventHubNamespace eventHubNamespace, string eventHubName)
    {
        return new(consumerGroup, partitionId, startingPosition, eventHubNamespace.FullyQualifiedNamespace, eventHubName, NoOpTokenCredential.Instance, eventHubNamespace.Provider);
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

    #region Get Properties

    public override async Task<PartitionProperties> GetPartitionPropertiesAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return await GetPartitionPropertiesCoreAsync(cancellationToken);
    }

    private async Task<PartitionProperties> GetPartitionPropertiesCoreAsync(CancellationToken cancellationToken)
    {
        var beforeContext = new GetPartitionPropertiesConsumerBeforeHookContext(_scope, Provider, cancellationToken);

        await ExecuteBeforeHooksAsync(beforeContext);

        var properties = GetPartition().GetProperties();

        var afterContext = new GetPartitionPropertiesConsumerAfterHookContext(beforeContext)
        {
            PartitionProperties = properties
        };

        await ExecuteAfterHooksAsync(afterContext);

        return properties;
    }

    public override LastEnqueuedEventProperties ReadLastEnqueuedEventProperties()
    {
        lock (_lastEnqueuedEventPropertiesLock)
        {
            if (_lastEnqueuedEventProperties is null)
            {
                return default;
            }

            return _lastEnqueuedEventProperties.Value;
        }

    }

    #endregion

    #region Receive Batch

    public override async Task<IEnumerable<EventData>> ReceiveBatchAsync(int maximumEventCount, TimeSpan maximumWaitTime, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return await ReceiveBatchCoreAsync(maximumEventCount, maximumWaitTime, cancellationToken);
    }

    public override async Task<IEnumerable<EventData>> ReceiveBatchAsync(int maximumEventCount, CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        return await ReceiveBatchCoreAsync(maximumEventCount, TimeSpan.FromSeconds(60), cancellationToken);
    }

    private async Task<IEnumerable<EventData>> ReceiveBatchCoreAsync(int maximumEventCount, TimeSpan maximumWaitTime, CancellationToken cancellationToken = default)
    {
        var beforeContext = new ReceiveBatchBeforeHookContext(_scope, Provider, cancellationToken);

        await ExecuteBeforeHooksAsync(beforeContext);

        var partition = GetPartition();

        var startTime = _timeProvider.GetTimestamp();

        IReadOnlyList<EventData> events;

        await _receiveLock.WaitAsync(cancellationToken);

        try
        {
            if (_position is null)
            {
                _position = _startingPosition;
            }

            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    events = [];
                    break;
                }

                if (!partition.TryGetEvents(_position.Value, maximumEventCount, out var fetchedEvents, out var nextPosition, out var error))
                {
                    throw error.GetClientException();
                }

                var elapsedTime = _timeProvider.GetElapsedTime(startTime);

                if (fetchedEvents.Count > 0 || elapsedTime > maximumWaitTime)
                {
                    _position = nextPosition;
                    events = fetchedEvents;
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
            }

            var partitionProperties = partition.GetProperties();

            lock (_lastEnqueuedEventPropertiesLock)
            {
                if (partitionProperties.IsEmpty)
                {
                    _lastEnqueuedEventProperties = null;
                }
                else
                {
                    _lastEnqueuedEventProperties = new(
                        partitionProperties.LastEnqueuedSequenceNumber,
                        partitionProperties.LastEnqueuedOffset,
                        partitionProperties.LastEnqueuedTime,
                        _timeProvider.GetUtcNow());
                }
            }
        }
        finally
        {
            _receiveLock.Release();
        }

        var afterContext = new ReceiveBatchAfterHookContext(beforeContext)
        {
            EventBatch = events
        };

        await ExecuteAfterHooksAsync(afterContext);

        return events;
    }

    #endregion

    private InMemoryPartition GetPartition()
    {
        var eh = EventHubClientUtils.GetEventHub(Provider, FullyQualifiedNamespace, EventHubName);

        EventHubClientUtils.HasConsumerGroupOrThrow(eh, ConsumerGroup);

        if (!eh.TryGetPartition(PartitionId, out var partition))
        {
            throw EventHubExceptionFactory.PartitionNotFound(eh, PartitionId);
        }

        return partition;

    }

    private Task ExecuteBeforeHooksAsync<TContext>(TContext context) where TContext : PartitionConsumerBeforeHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

    private Task ExecuteAfterHooksAsync<TContext>(TContext context) where TContext : PartitionConsumerAfterHookContext
    {
        return Provider.ExecuteHooksAsync(context);
    }

}
