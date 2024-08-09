namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;

public abstract class EventHubAfterHookContext(EventHubBeforeHookContext before)
    : EventHubHookContext(before.EventHubNamespaceName, before.EventHubName, before.ResouceProvider, before.CancellationToken);
