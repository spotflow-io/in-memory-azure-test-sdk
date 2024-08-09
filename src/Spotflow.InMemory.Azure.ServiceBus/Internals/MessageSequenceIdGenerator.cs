namespace Spotflow.InMemory.Azure.ServiceBus.Internals;

internal class MessageSequenceIdGenerator
{
    private long _next = -1;

    public long GetNext(int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        var lastSequenceNumber = Interlocked.Add(ref _next, count);
        var firstSequenceNumber = lastSequenceNumber - count + 1;

        return firstSequenceNumber;
    }
}
