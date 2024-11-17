namespace Spotflow.InMemory.Azure.ServiceBus.Resources;

public class InMemoryServiceBusQueueOptions
{
    public bool EnableSessions { get; init; } = false;
    public TimeSpan? LockTime { get; init; } = null;
}
