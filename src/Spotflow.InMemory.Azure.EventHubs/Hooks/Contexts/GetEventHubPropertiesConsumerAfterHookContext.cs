using Azure.Messaging.EventHubs;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class GetEventHubPropertiesConsumerAfterHookContext(GetEventHubPropertiesConsumerBeforeHookContext before)
    : ConsumerAfterHookContext(before)
{
    public required EventHubProperties EventHubProperties { get; init; }
    public GetEventHubPropertiesConsumerBeforeHookContext BeforeContext => before;
}
