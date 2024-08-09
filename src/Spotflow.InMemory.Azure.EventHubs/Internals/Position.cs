namespace Spotflow.InMemory.Azure.EventHubs.Internals;


internal readonly record struct Position(long SequenceNumber, bool IsInclusive)
{
    public static Position FromSequenceNumber(long sequenceNumber, bool isInclusive) => new(sequenceNumber, isInclusive);
}
