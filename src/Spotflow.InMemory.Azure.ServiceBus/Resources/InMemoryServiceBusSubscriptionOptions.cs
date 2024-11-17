namespace Spotflow.InMemory.Azure.ServiceBus.Resources;

public class InMemoryServiceBusSubscriptionOptions
{
    public bool EnableSessions { get; init; } = false;
    public TimeSpan? LockTime { get; init; } = null;
}
