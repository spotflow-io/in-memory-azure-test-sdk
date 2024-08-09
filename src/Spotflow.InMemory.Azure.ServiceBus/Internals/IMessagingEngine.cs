using System.Diagnostics.CodeAnalysis;

using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Internals;

internal interface IMessagingEngine
{
    bool TryAddMessage(ServiceBusMessage message, [NotNullWhen(false)] out AddMessageError? error);
    bool TryAddMessages(IReadOnlyList<ServiceBusMessage> messages, [NotNullWhen(false)] out AddMessageError? error);
    long ActiveMessageCount { get; }
    long MessageCount { get; }
}
