namespace Spotflow.InMemory.Azure.ServiceBus.Resources;

public record InMemoryServiceBusSubscriptionOptions(bool EnableSessions = false, TimeSpan? LockTime = null);
