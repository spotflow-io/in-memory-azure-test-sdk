using System.Globalization;

using Azure.Messaging.EventHubs.Consumer;

using Spotflow.InMemory.Azure.Internals;

namespace Spotflow.InMemory.Azure.EventHubs.Internals;

internal readonly record struct InMemoryEventPosition(long SequenceNumber, bool IsInclusive, bool IsEarliest, bool IsLatest, bool IsWaitingForNewEvents = false)
{
    public static InMemoryEventPosition FromSequenceNumber(long sequenceNumber, bool isInclusive, bool isWaitingForNewEvents = false)
    {
        return new(sequenceNumber, isInclusive, false, false, IsWaitingForNewEvents: isWaitingForNewEvents);
    }

    public static InMemoryEventPosition Earliest => new(-1, false, true, false);
    public static InMemoryEventPosition Latest => new(-1, false, false, true);

    public static InMemoryEventPosition FromEventPosition(EventPosition position)
    {
        if (position == EventPosition.Earliest)
        {
            return InMemoryEventPosition.Earliest;
        }

        if (position == EventPosition.Latest)
        {
            return InMemoryEventPosition.Latest;
        }

        var sequenceNumberObj = ReflectionUtils.ReadInternalReferenceProperty<object>(position, "SequenceNumber");

        long? sequencenceNumber = sequenceNumberObj switch
        {
            long l => l,
            null => null,
            string s => long.Parse(s, CultureInfo.InvariantCulture),
            _ => throw new InvalidOperationException($"SequenceNumber property with value '{sequenceNumberObj}' has unexpected type: {sequenceNumberObj?.GetType()}.")
        };

        if (sequencenceNumber is null)
        {
            throw new InvalidOperationException("SequenceNumber property not available.");
        }

        var isInclusive = ReflectionUtils.ReadInternalValueProperty<bool>(position, "IsInclusive");

        if (ReflectionUtils.TryReadOptionalInternalReferenceProperty<object>(position, "OffsetString", out var offsetString) &&
            offsetString is not null)
        {
            throw new NotSupportedException("EventPosition with offset is not supported.");
        }

        if (ReflectionUtils.TryReadOptionalInternalReferenceProperty<object>(position, "Offset", out var offset) &&
            offset is not null)
        {
            throw new NotSupportedException("EventPosition with offset is not supported.");
        }

        return InMemoryEventPosition.FromSequenceNumber(sequencenceNumber.Value, isInclusive);
    }
}
