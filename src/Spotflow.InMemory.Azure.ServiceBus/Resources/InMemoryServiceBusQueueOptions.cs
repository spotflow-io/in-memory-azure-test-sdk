namespace Spotflow.InMemory.Azure.ServiceBus.Resources;

public record InMemoryServiceBusQueueOptions(bool EnableSessions = false, TimeSpan? LockTime = null);
