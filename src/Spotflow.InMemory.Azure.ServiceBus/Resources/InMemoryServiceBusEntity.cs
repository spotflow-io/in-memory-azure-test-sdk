using System.Diagnostics.CodeAnalysis;

using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Logging;

using Spotflow.InMemory.Azure.ServiceBus.Internals;

namespace Spotflow.InMemory.Azure.ServiceBus.Resources;

public abstract class InMemoryServiceBusEntity(InMemoryServiceBusNamespace parentNamespace)
{
    public abstract string EntityPath { get; }

    public string FullyQualifiedNamespace => Namespace.FullyQualifiedNamespace;
    public InMemoryServiceBusProvider Provider => Namespace.Provider;
    public InMemoryServiceBusNamespace Namespace { get; } = parentNamespace;
    public TimeProvider TimeProvider => Namespace.TimeProvider;

    internal ILoggerFactory LoggerFactory => Namespace.LoggerFactory;

    internal static TimeSpan DefaultLockTime = TimeSpan.FromSeconds(30);

    internal abstract bool TryAddMessage(ServiceBusMessage message, [NotNullWhen(false)] out AddMessageError? error);

    internal abstract bool TryAddMessages(IReadOnlyList<ServiceBusMessage> messages, [NotNullWhen(false)] out AddMessageError? error);
}


