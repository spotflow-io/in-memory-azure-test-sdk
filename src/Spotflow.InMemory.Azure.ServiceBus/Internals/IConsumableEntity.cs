namespace Spotflow.InMemory.Azure.ServiceBus.Internals;

internal interface IConsumableEntity
{
    string FullyQualifiedNamespace { get; }

    string EntityPath { get; }
    bool EnableSessions { get; }
    TimeSpan LockTime { get; }
    long ActiveMessageCount { get; }
    long MessageCount { get; }
    TimeProvider TimeProvider { get; }

    IMessagingEngine Engine { get; }
}
