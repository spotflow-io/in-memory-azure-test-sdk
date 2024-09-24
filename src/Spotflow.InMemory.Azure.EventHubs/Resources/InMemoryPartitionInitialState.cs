namespace Spotflow.InMemory.Azure.EventHubs.Resources;

public class InMemoryPartitionInitialState
{
    public InMemoryPartitionInitialState(long sequenceNumber, long offset, DateTimeOffset enqueuedTime)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequenceNumber, -1);
        ArgumentOutOfRangeException.ThrowIfLessThan(offset, -1);

        SequenceNumber = sequenceNumber;
        Offset = offset;
        EnqueuedTime = enqueuedTime;
    }

    public long SequenceNumber { get; }
    public long Offset { get; }
    public DateTimeOffset EnqueuedTime { get; }

    public static InMemoryPartitionInitialState Default { get; } = new(-1, -1, DateTimeOffset.MinValue);
}
