using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.Hooks.Internals;
using Spotflow.InMemory.Azure.ServiceBus.Hooks;
using Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;
using Spotflow.InMemory.Azure.ServiceBus.Hooks.Internals;
using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus;
public class InMemoryServiceBusProvider(TimeProvider? timeProvider = null, string? hostnameSuffix = null)
{
    private readonly ConcurrentDictionary<string, InMemoryServiceBusNamespace> _namespaces = new();

    public TimeProvider TimeProvider { get; } = timeProvider ?? TimeProvider.System;

    public string HostnameSuffix { get; } = hostnameSuffix ?? "servicebus.in-memory.example.com";

    private readonly HooksExecutor<ServiceBusHookFilter, ServiceBusHookContext> _hooksExecutor = new();

    public InMemoryServiceBusNamespace AddNamespace(string? namespaceName = null)
    {
        if (namespaceName is not null && namespaceName.Contains('.'))
        {
            throw new ArgumentException($"Namespace name cannot contain dots. Got '{namespaceName}'");
        }

        namespaceName ??= GenerateNamespaceName();

        namespaceName = namespaceName.ToLowerInvariant();

        var ns = new InMemoryServiceBusNamespace(namespaceName, this);

        if (!_namespaces.TryAdd(ns.FullyQualifiedNamespace, ns))
        {
            throw new InvalidOperationException($"Service bus namespace '{namespaceName}' already added.");
        }

        return ns;
    }

    public bool TryGetNamespace(string namespaceName, [NotNullWhen(true)] out InMemoryServiceBusNamespace? result)
    {
        namespaceName = namespaceName.ToLowerInvariant();

        foreach (var (_, ns) in _namespaces)
        {
            if (ns.Name == namespaceName)
            {
                result = ns;
                return true;
            }
        }

        result = null;
        return false;
    }

    public InMemoryServiceBusNamespace GetNamespace(string namespaceName)
    {
        namespaceName = namespaceName.ToLowerInvariant();

        if (!TryGetNamespace(namespaceName, out var serviceBusNamespace))
        {
            throw new InvalidOperationException($"Service bus namespace '{namespaceName}' not found.");
        }

        return serviceBusNamespace;
    }

    public bool TryGetFullyQualifiedNamespace(string fullyQualifiedNamespace, [NotNullWhen(true)] out InMemoryServiceBusNamespace? result)
    {
        fullyQualifiedNamespace = fullyQualifiedNamespace.ToLowerInvariant();

        return _namespaces.TryGetValue(fullyQualifiedNamespace, out result);
    }

    public InMemoryServiceBusNamespace GetFullyQualifiedNamespace(string fullyQualifiedNamespace)
    {
        if (!TryGetFullyQualifiedNamespace(fullyQualifiedNamespace, out var result))
        {
            throw new InvalidOperationException($"Service bus namespace with fully qualified name '{fullyQualifiedNamespace}' not found.");
        }

        return result;
    }

    public IHookRegistration AddHook<TContext>(Func<ServiceBusHookBuilder, ServiceBusHook<TContext>> buildHook) where TContext : ServiceBusHookContext
    {
        var hook = buildHook(new());

        return _hooksExecutor.AddHook(hook.HookFunction, hook.Filter);
    }

    internal Task ExecuteHooksAsync<TContext>(TContext context) where TContext : ServiceBusHookContext
    {
        return _hooksExecutor.ExecuteHooksAsync(context);
    }

    internal string GetNamespaceNameFromHostname(string hostname)
    {
        if (!hostname.EndsWith(HostnameSuffix))
        {
            throw new FormatException($"Service Bus namespace host name is expected to end with '{HostnameSuffix}'. Got {hostname}.");
        }

        return hostname[..(hostname.Length - HostnameSuffix.Length - 1)];
    }

    private static string GenerateNamespaceName() => Guid.NewGuid().ToString();
}
