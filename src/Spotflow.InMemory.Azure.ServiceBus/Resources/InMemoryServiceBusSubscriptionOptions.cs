using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Resources;

public class InMemoryServiceBusSubscriptionOptions
{
    public bool EnableSessions { get; init; } = false;
    public TimeSpan? LockTime { get; init; } = null;

    /// <summary>
    /// Optional filter evaluated for every message sent to the parent topic.
    /// Only messages for which the filter returns <c>true</c> are added to the subscription.
    /// When not set, all messages are accepted (equivalent to the default <c>TrueFilter</c> rule).
    /// </summary>
    /// <remarks>
    /// Use this to emulate subscription rules (e.g. SQL or correlation filters),
    /// typically by inspecting <see cref="ServiceBusMessage.ApplicationProperties"/>.
    /// </remarks>
    public Func<ServiceBusMessage, bool>? Filter { get; init; } = null;
}
