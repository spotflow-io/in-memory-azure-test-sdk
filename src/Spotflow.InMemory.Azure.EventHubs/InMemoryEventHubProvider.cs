using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Spotflow.InMemory.Azure.EventHubs.Hooks;
using Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;
using Spotflow.InMemory.Azure.EventHubs.Hooks.Internals;
using Spotflow.InMemory.Azure.EventHubs.Resources;
using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.Hooks.Internals;

namespace Spotflow.InMemory.Azure.EventHubs;

public class InMemoryEventHubProvider(TimeProvider? timeProvider = null, string? hostnameSuffix = null)
{
    private readonly ConcurrentDictionary<string, InMemoryEventHubNamespace> _namespaces = new(StringComparer.OrdinalIgnoreCase);

    private readonly HooksExecutor<EventHubHookFilter, EventHubHookContext> _hooksExecutor = new();

    public string HostnameSuffix { get; } = hostnameSuffix ?? "eventhub.in-memory.example.com";

    internal TimeProvider TimeProvider { get; } = timeProvider ?? TimeProvider.System;


    public InMemoryEventHubNamespace AddNamespace(string? namespaceName = null)
    {
        if (namespaceName is not null && namespaceName.Contains('.'))
        {
            throw new ArgumentException($"Namespace name cannot contain dots. Got '{namespaceName}'");
        }


        namespaceName ??= GenerateNamespaceName();

        var ns = new InMemoryEventHubNamespace(namespaceName, this);

        if (!_namespaces.TryAdd(ns.FullyQualifiedNamespace, ns))
        {
            throw new InvalidOperationException($"Event Hub namespace '{namespaceName}' already exists.");
        }

        return ns;
    }

    public bool TryGetNamespace(string name, [NotNullWhen(true)] out InMemoryEventHubNamespace? result)
    {
        foreach (var (_, ns) in _namespaces)
        {
            if (ns.Name == name)
            {
                result = ns;
                return true;
            }
        }

        result = null;
        return false;
    }

    public InMemoryEventHubNamespace GetNamespace(string name)
    {
        if (!TryGetNamespace(name, out var result))
        {
            throw new InvalidOperationException($"Event Hub namespace '{name}' not found.");
        }

        return result;
    }

    public bool TryGetFullyQualifiedNamespace(string fullyQualifiedNamespace, [NotNullWhen(true)] out InMemoryEventHubNamespace? result)
    {
        return _namespaces.TryGetValue(fullyQualifiedNamespace, out result);
    }


    public InMemoryEventHubNamespace GetFullyQualifiedNamespace(string fullyQualifiedNamespace)
    {
        if (TryGetFullyQualifiedNamespace(fullyQualifiedNamespace, out var result))
        {
            return result;
        }

        throw new InvalidOperationException($"Event Hub namespace with hostname '{fullyQualifiedNamespace}' not found.");
    }

    public IHookRegistration AddHook<TContext>(Func<EventHubHookBuilder, EventHubHook<TContext>> buildHook) where TContext : EventHubHookContext
    {
        var hook = buildHook(new());

        return _hooksExecutor.AddHook(hook.HookFunction, hook.Filter);
    }

    internal Task ExecuteHooksAsync<TContext>(TContext context, CancellationToken cancellationToken) where TContext : EventHubHookContext
    {
        return _hooksExecutor.ExecuteHooksAsync(context);
    }

    internal string GetNamespaceNameFromHostname(string hostname)
    {
        if (!hostname.EndsWith(HostnameSuffix))
        {
            throw new FormatException($"Event Hub namespace host name is expected to end with '{HostnameSuffix}'. Got {hostname}.");
        }


        return hostname[..(hostname.Length - HostnameSuffix.Length - 1)];
    }

    private static string GenerateNamespaceName() => Guid.NewGuid().ToString();
}




