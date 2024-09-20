using System.Diagnostics.CodeAnalysis;

using Azure.Messaging.EventHubs;

using Spotflow.InMemory.Azure.EventHubs.Resources;

namespace Spotflow.InMemory.Azure.EventHubs.Internals;

internal class InMemoryPartition
{
    private readonly object _syncObj = new();

    private readonly TimeProvider _timeProvider;
    private readonly long _initialSequenceNumber;

    private EventData[] _events = new EventData[1024];
    private int _eventCount = 0;
    private int _trimCount = 0;

    private long _lastSequenceNumber;
    private long _lastOffset;
    private DateTimeOffset _lastEnqueuedTime;
    private long _previousEventBodyLenght;

    public InMemoryPartition(string partitionId, InMemoryPartitionInitialState? initialState, InMemoryEventHub eventHub)
    {
        PartitionId = partitionId;
        EventHub = eventHub;

        _timeProvider = eventHub.Provider.TimeProvider;

        initialState ??= InMemoryPartitionInitialState.Default;

        _initialSequenceNumber = initialState.SequenceNumber;

        _lastSequenceNumber = initialState.SequenceNumber;
        _lastOffset = initialState.Offset;
        _lastEnqueuedTime = initialState.EnqueuedTime;

    }

    public string PartitionId { get; }

    public InMemoryEventHub EventHub { get; }

    public void TriggerRetentionPolicy(int deleteCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(deleteCount);

        lock (_syncObj)
        {
            _trimCount += deleteCount;
        }
    }

    public PartitionProperties GetProperties()
    {
        var name = EventHub.Properties.Name;

        long lastSequenceNumber;
        long lastOffset;
        DateTimeOffset lastEnqueuedTime;
        ArraySegment<EventData> currentSegment;

        lock (_syncObj)
        {
            currentSegment = GetCurrentEventsSegmentUnsafe();
            lastSequenceNumber = _lastSequenceNumber;
            lastOffset = _lastOffset;
            lastEnqueuedTime = _lastEnqueuedTime;
        }

        var beginningSequenceNumber = currentSegment.Count > 0 ? currentSegment[0].SequenceNumber : lastSequenceNumber;

        return EventHubsModelFactory.PartitionProperties(
              eventHubName: name,
              partitionId: PartitionId,
              isEmpty: currentSegment.Count is 0,
              beginningSequenceNumber: beginningSequenceNumber,
              lastSequenceNumber: _lastSequenceNumber,
              lastOffset: _lastOffset,
              lastEnqueuedTime: _lastEnqueuedTime);
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

            var eventBodyMemory = eventData.EventBody.ToMemory();
            var eventBodyCopy = new byte[eventBodyMemory.Length];
            eventBodyMemory.CopyTo(eventBodyCopy);

            _lastSequenceNumber += 1;
            _lastEnqueuedTime = eventData.EnqueuedTime != default ? eventData.EnqueuedTime : _timeProvider.GetUtcNow();

            if (_lastOffset is -1)
            {
                _lastOffset = 0;
            }
            else
            {
                _lastOffset += _previousEventBodyLenght;
            }

            var eventDataPropertiesCopy = new Dictionary<string, object>(eventData.Properties);
            var eventDataSystemPropertiesCopy = new Dictionary<string, object>(eventData.SystemProperties);

            var eventWithSystemProperties = EventHubsModelFactory.EventData(
                eventBody: new(eventBodyCopy),
                properties: eventDataPropertiesCopy,
                systemProperties: eventDataSystemPropertiesCopy,
                partitionKey: partitionKey,
                sequenceNumber: _lastSequenceNumber,
                offset: _lastOffset,
                enqueuedTime: _lastEnqueuedTime
               );

            eventWithSystemProperties.MessageId = eventData.MessageId;
            eventWithSystemProperties.CorrelationId = eventData.CorrelationId;
            eventWithSystemProperties.ContentType = eventData.ContentType;

            _events[_eventCount++] = eventWithSystemProperties;

            _previousEventBodyLenght = CalculateEventSize(eventWithSystemProperties);

        }

    }

    public bool TryGetEvents(
        InMemoryEventPosition position,
        int maximumEventCount,
        [NotNullWhen(true)] out IReadOnlyList<EventData>? events,
        [NotNullWhen(true)] out InMemoryEventPosition? nextPosition,
        [NotNullWhen(false)] out TryGetEventsError? error
        )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEventCount);

        ArraySegment<EventData> currentEventsSegment;
        long trimCount;
        long lastSequenceNumber;

        lock (_syncObj)
        {
            currentEventsSegment = GetCurrentEventsSegmentUnsafe();
            trimCount = _trimCount;
            lastSequenceNumber = _lastSequenceNumber;
        }

        var beginningSequenceNumber = currentEventsSegment.Count > 0 ? currentEventsSegment[0].SequenceNumber : lastSequenceNumber;

        long startSequenceNumber;

        if (position == InMemoryEventPosition.Earliest)
        {
            startSequenceNumber = beginningSequenceNumber;
        }
        else if (position == InMemoryEventPosition.Latest)
        {
            events = [];
            nextPosition = InMemoryEventPosition.FromSequenceNumber(lastSequenceNumber, isInclusive: false, isWaitingForNewEvents: true);
            error = null;
            return true;
        }
        else
        {
            startSequenceNumber = position.IsInclusive ? position.SequenceNumber : position.SequenceNumber + 1;
        }

        if (startSequenceNumber > lastSequenceNumber)
        {
            if (position.IsWaitingForNewEvents || !position.IsInclusive)
            {
                events = [];
                nextPosition = InMemoryEventPosition.FromSequenceNumber(lastSequenceNumber, isInclusive: false, isWaitingForNewEvents: true);
                error = null;
                return true;
            }
            else
            {
                events = null;
                nextPosition = null;
                error = new TryGetEventsError.InvalidStartingSequenceNumber(position, lastSequenceNumber);
                return false;
            }
        }

        if (currentEventsSegment.Count is 0)
        {
            events = [];
            nextPosition = InMemoryEventPosition.FromSequenceNumber(lastSequenceNumber, isInclusive: false, isWaitingForNewEvents: true);
            error = null;
            return true;
        }

        if (startSequenceNumber < beginningSequenceNumber)
        {
            startSequenceNumber = beginningSequenceNumber;
        }

        var startSequenceNumberNormalized = startSequenceNumber - _initialSequenceNumber - 1 - trimCount;

        if (startSequenceNumberNormalized >= currentEventsSegment.Count)
        {
            throw new InvalidOperationException($"Start sequence number ({startSequenceNumber}) is larger or equal to count {currentEventsSegment.Count}.");
        }

        // Number is surely less than int.MaxValue so the conversaion is safe.
        var startSequenceNumberNormalizedAsInt = (int) startSequenceNumberNormalized;

        var end = startSequenceNumberNormalizedAsInt + maximumEventCount;

        if (end > currentEventsSegment.Count)
        {
            end = currentEventsSegment.Count;
        }

        events = currentEventsSegment[startSequenceNumberNormalizedAsInt..end];
        nextPosition = InMemoryEventPosition.FromSequenceNumber(events[^1].SequenceNumber, isInclusive: false, isWaitingForNewEvents: true);
        error = null;
        return true;
    }


    private ArraySegment<EventData> GetCurrentEventsSegmentUnsafe()
    {
        lock (_syncObj)
        {
            return new(_events, _trimCount, _eventCount - _trimCount);
        }
    }

    public abstract class TryGetEventsError
    {
        public abstract Exception GetClientException();

        public class InvalidStartingSequenceNumber(InMemoryEventPosition requested, long last) : TryGetEventsError
        {
            public override Exception GetClientException()
            {
                return EventHubExceptionFactory.InvalidStartingSequenceNumber(requested, last);
            }
        }
    }

    private static long CalculateEventSize(EventData eventData)
    {
        long size = eventData.EventBody.ToMemory().Length;

        size += 8; // SequenceNumber
        size += 8; // Offset
        size += 10; // EnqueuedTime
        size += eventData.MessageId?.Length ?? 0;
        size += eventData.ContentType?.Length ?? 0;
        size += eventData.CorrelationId?.Length ?? 0;
        size += eventData.PartitionKey?.Length ?? 0;

        size += sizeOfProperties(eventData.Properties);
        size += sizeOfProperties(eventData.SystemProperties);

        return size;

        static long sizeOfProperties(IEnumerable<KeyValuePair<string, object>> properties)
        {
            var size = 0L;

            foreach (var (key, value) in properties)
            {
                var valueSize = value switch
                {
                    string s => s.Length,
                    byte[] b => b.Length,
                    int => sizeof(int),
                    long => sizeof(long),
                    float => sizeof(float),
                    double => sizeof(double),
                    decimal => sizeof(decimal),
                    bool => sizeof(bool),
                    DateTimeOffset => 10,
                    DateTime => 10,
                    Guid g => 16,
                    var obj => obj.ToString()?.Length ?? 0
                };

                size += key.Length + valueSize;
            }

            return size;
        }
    }

}
