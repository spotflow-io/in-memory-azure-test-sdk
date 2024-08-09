using Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;
using Spotflow.InMemory.Azure.Hooks;

namespace Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;

internal record EventHubHookFilter : BaseHookFilter<EventHubHookContext>
{
    public string? EventHubNamespaceName { get; private init; }
    public string? EventHubName { get; private init; }

    public override bool Covers(EventHubHookContext context)
    {
        var result = true;

        result &= EventHubNamespaceName is null || EventHubNamespaceName == context.EventHubNamespaceName;
        result &= EventHubName is null || EventHubName == context.EventHubName;

        return result;
    }

    public EventHubHookFilter With(string? eventHubNamespaceName, string? eventHubName)
    {
        return this with
        {
            EventHubNamespaceName = eventHubNamespaceName ?? EventHubNamespaceName,
            EventHubName = eventHubName ?? EventHubName
        };
    }
}
