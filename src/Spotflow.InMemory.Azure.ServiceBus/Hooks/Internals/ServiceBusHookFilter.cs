using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.ServiceBus.Hooks.Internals;

internal record ServiceBusHookFilter : BaseHookFilter<ServiceBusHookContext>
{
    public string? ServiceBusNamespaceName { get; init; }
    public string? EntityPath { get; init; }

    public override bool Covers(ServiceBusHookContext context)
    {
        var result = true;

        result &= ServiceBusNamespaceName == null || context.ServiceBusNamespaceName == ServiceBusNamespaceName;
        result &= EntityPath == null || context.EntityPath == EntityPath;

        return result;
    }

    public ServiceBusHookFilter With(string? serviceBusNamespaceName, string? entityPath)
    {
        return this with
        {
            ServiceBusNamespaceName = serviceBusNamespaceName ?? ServiceBusNamespaceName,
            EntityPath = entityPath ?? EntityPath
        };
    }
}
