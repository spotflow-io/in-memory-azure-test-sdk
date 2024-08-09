using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class SendBeforeHookContext(ProducerEventHubScope scope, InMemoryEventHubProvider provider, CancellationToken cancellationToken)
    : ProducerBeforeHookContext(scope, ProducerOperations.Send, provider, cancellationToken)
{
    public required IReadOnlyList<EventData> EventBatch { get; init; }
    public required SendEventOptions? SendEventOptions { get; init; }
}
