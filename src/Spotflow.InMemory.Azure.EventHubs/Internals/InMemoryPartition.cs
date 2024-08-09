using Azure.Messaging.EventHubs;

using Spotflow.InMemory.Azure.EventHubs.Resources;

namespace Spotflow.InMemory.Azure.EventHubs.Internals;
internal class InMemoryPartition
{
    private readonly object _syncObj = new();

    private readonly TimeProvider _timeProvider;
    private EventData[] _events = new EventData[1024];
    private int _eventCount = 0;
    private long _eventOffset = 0;

    public InMemoryPartition(string partitionId, long initialSequenceNumber, InMemoryEventHub eventHub)
    {
        PartitionId = partitionId;
        EventHub = eventHub;

        if (initialSequenceNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialSequenceNumber), initialSequenceNumber, "Initial sequence number must be greater than or equal to 0.");
        }

        InitialSequenceNumber = initialSequenceNumber;
        _timeProvider = eventHub.Provider.TimeProvider;
    }

    public long LastSequenceNumber
    {
        get
        {
            lock (_syncObj)
            {
                return InitialSequenceNumber + _eventCount - 1;
            }
        }
    }

    public string PartitionId { get; }

    public InMemoryEventHub EventHub { get; }

    public long InitialSequenceNumber { get; }



    public PartitionProperties GetProperties()
    {
        var name = EventHub.Properties.Name;

        var eventDataSegment = GetCurrentEventsSegment();

        if (eventDataSegment.Count is 0)
        {
            return EventHubsModelFactory.PartitionProperties(name, PartitionId, true, -1, -1, -1, DateTimeOffset.MinValue);
        }
        else
        {
            var first = eventDataSegment[0];
            var last = eventDataSegment[^1];

            return EventHubsModelFactory.PartitionProperties(name, PartitionId, false, first.SequenceNumber, last.SequenceNumber, last.Offset, last.EnqueuedTime);
        }
    }

    public void SendEvent(EventData eventData, string? partitionKey)
    {
        lock (_syncObj)
        {
            if (_events.Length == _eventCount)
            {
                var newEvents = new EventData[_events.Length * 2];

                Array.Copy(_events, newEvents, _events.Length); // Do not zero or reuse old array because it might be still in use.

                _events = newEvents;
            }

            var sequenceNumber = InitialSequenceNumber + _eventCount;

            var enqueuedTime = eventData.EnqueuedTime != default ? eventData.EnqueuedTime : _timeProvider.GetUtcNow();

            var eventBodyMemory = eventData.EventBody.ToMemory();

            var eventBodyCopy = new byte[eventBodyMemory.Length];
            eventBodyMemory.CopyTo(eventBodyCopy);

            var eventDataPropertiesCopy = new Dictionary<string, object>(eventData.Properties);
            var eventDataSystemPropertiesCopy = new Dictionary<string, object>(eventData.SystemProperties);

            var eventWithSystemProperties = EventHubsModelFactory.EventData(
                eventBody: new(eventBodyCopy),
                properties: eventDataPropertiesCopy,
                systemProperties: eventDataSystemPropertiesCopy,
                partitionKey: partitionKey,
                sequenceNumber: sequenceNumber,
                offset: _eventOffset,
                enqueuedTime: enqueuedTime
               );

            eventWithSystemProperties.MessageId = eventData.MessageId;
            eventWithSystemProperties.CorrelationId = eventData.CorrelationId;
            eventWithSystemProperties.ContentType = eventData.ContentType;

            _events[_eventCount++] = eventWithSystemProperties;
            _eventOffset += eventBodyMemory.Length;
        }

    }

    public IReadOnlyList<EventData> GetEvents(Position position, int maximumEventCount)
    {
        var startSequenceNumber = position.IsInclusive ? position.SequenceNumber : position.SequenceNumber + 1;
        return GetEventsCore(startSequenceNumber, maximumEventCount);
    }

    private IReadOnlyList<EventData> GetEventsCore(long startSequenceNumber, int maximumEventCount)
    {
        var currentEventsSegment = GetCurrentEventsSegment();

        var startSequenceNumberNormalized = startSequenceNumber - InitialSequenceNumber;

        if (startSequenceNumberNormalized >= currentEventsSegment.Count)
        {
            return [];
        }

        // Number is surely less than int.MaxValue so the conversaion is safe.

        var startSequenceNumberNormalizedAsInt = (int) startSequenceNumberNormalized;

        var end = startSequenceNumberNormalizedAsInt + maximumEventCount;

        if (end > currentEventsSegment.Count)
        {
            end = currentEventsSegment.Count;
        }

        return currentEventsSegment[startSequenceNumberNormalizedAsInt..end];
    }


    private ArraySegment<EventData> GetCurrentEventsSegment()
    {
        lock (_syncObj)
        {
            return new(_events, 0, _eventCount);
        }
    }

    public Position ResolvePosition(StartingPosition startingPosition)
    {
        if (startingPosition == StartingPosition.Earliest)
        {
            return Position.FromSequenceNumber(InitialSequenceNumber, true);
        }

        if (startingPosition == StartingPosition.Latest)
        {
            return Position.FromSequenceNumber(LastSequenceNumber, false);
        }

        return Position.FromSequenceNumber(startingPosition.SequenceNumber, startingPosition.IsInclusive);
    }
}
