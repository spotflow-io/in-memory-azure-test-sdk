using Azure.Messaging.EventHubs;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public class GetConsumerEventHubPropertiesAfterHookContext(GetConsumerEventHubPropertiesBeforeHookContext before) : ConsumerAfterHookContext(before)
{
    public required EventHubProperties EventHubProperties { get; init; }
    public GetConsumerEventHubPropertiesBeforeHookContext BeforeContext => before;
}
