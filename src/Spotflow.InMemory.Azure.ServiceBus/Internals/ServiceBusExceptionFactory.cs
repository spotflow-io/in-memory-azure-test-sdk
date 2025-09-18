using System.Runtime.CompilerServices;

using Azure.Messaging.ServiceBus;

namespace Spotflow.InMemory.Azure.ServiceBus.Internals;

internal static class ServiceBusExceptionFactory
{
    public static NotSupportedException MethodNotSupported([CallerMemberName] string? callerMemberName = null)
    {
        return new($"In-memory service bus client does not support method '{callerMemberName}'.");
    }

    public static NotSupportedException FeatureNotSupported(string featureName)
    {
        return new($"In-memory service bus client does not support feature '{featureName}'.");
    }

    public static ServiceBusException MessageLockLost(string fullyQualifiedNamespace, string entityPath)
    {
        return new("Message lock lost.", ServiceBusFailureReason.MessageLockLost, EntityFullPath(fullyQualifiedNamespace, entityPath));
    }

    public static ServiceBusException NamespaceNotFound(string fullyQualifiedNamespace)
    {
        return new(
            isTransient: true,
            $"No such host is known. ErrorCode: HostNotFound ({fullyQualifiedNamespace})",
            null,
            ServiceBusFailureReason.ServiceCommunicationProblem);
    }

    public static ServiceBusException MessagingEntityNotFound(string fullyQualifiedNamespace, string entityPath)
    {
        return new("Messaging entity not found.", ServiceBusFailureReason.MessagingEntityNotFound, EntityFullPath(fullyQualifiedNamespace, entityPath));
    }

    public static ServiceBusException NoSessionAvailable(string fullyQualifiedNamespace, string entityPath)
    {
        return new("No session available.", ServiceBusFailureReason.ServiceTimeout, EntityFullPath(fullyQualifiedNamespace, entityPath));
    }

    public static InvalidOperationException SessionIdNotSetOnMessage(string fullyQualifiedNamespace, string entityPath)
    {
        return new(
            "" +
            "The SessionId was not set on a message, and it cannot be sent to the entity. " +
            "Entities that have session support enabled can only " +
            "receive messages that have the SessionId set to a valid value. " +
            $"Entity = {EntityFullPath(fullyQualifiedNamespace, entityPath)}"
            );
    }

    public static ServiceBusException SessionNotFound(string fullyQualifiedNamespace, string entityPath, string sessionId)
    {
        var path = EntityFullPath(fullyQualifiedNamespace, entityPath);
        return new($"Session {sessionId} not found.", ServiceBusFailureReason.GeneralError, path);
    }

    public static ServiceBusException SessionCannotBeLocked(string fullyQualifiedNamespace, string entityPath, string sessionId)
    {
        var message = $"The requested session '{sessionId}' cannot be accepted. It may be locked by another receiver.";
        var path = EntityFullPath(fullyQualifiedNamespace, entityPath);
        return new(message, ServiceBusFailureReason.SessionCannotBeLocked, path);
    }

    public static ServiceBusException SessionsEnabled(string fullyQualifiedNamespace, string entityPath)
    {
        var path = EntityFullPath(fullyQualifiedNamespace, entityPath);
        return new($"Session is required but message does not contain session id.", ServiceBusFailureReason.GeneralError, path);
    }

    public static ServiceBusException SessionsNotEnabled(string fullyQualifiedNamespace, string entityPath)
    {
        return new("Sessions are not enabled.", ServiceBusFailureReason.GeneralError, EntityFullPath(fullyQualifiedNamespace, entityPath));
    }

    public static ServiceBusException SessionReceiveFailed(ServiceBusFailureReason reason, string fullyQualifiedNamespace, string entityPath, string sessionId)
    {
        return reason switch
        {
            ServiceBusFailureReason.SessionLockLost => SessionLockLost(fullyQualifiedNamespace, entityPath, sessionId),
            _ => throw new InvalidOperationException($"Unexpected failure reason: {reason}"),
        };
    }

    public static Exception SessionRenewFailed(ServiceBusFailureReason reason, string fullyQualifiedNamespace, string entityPath, string sessionId)
    {
        return reason switch
        {
            ServiceBusFailureReason.SessionLockLost => SessionLockLost(fullyQualifiedNamespace, entityPath, sessionId),
            _ => throw new InvalidOperationException($"Unexpected failure reason: {reason}"),
        };
    }

    public static ServiceBusException SessionStateGetSetFailed(ServiceBusFailureReason reason, string fullyQualifiedNamespace, string entityPath, string sessionId)
    {
        return reason switch
        {
            ServiceBusFailureReason.SessionLockLost => SessionLockLost(fullyQualifiedNamespace, entityPath, sessionId),
            _ => throw new InvalidOperationException($"Unexpected failure reason: {reason}"),
        };
    }

    public static ServiceBusException SessionRenewMessageFailed(ServiceBusFailureReason reason, string fullyQualifiedNamespace, string entityPath, string sessionId)
    {
        return reason switch
        {
            ServiceBusFailureReason.SessionLockLost => SessionLockLost(fullyQualifiedNamespace, entityPath, sessionId),
            ServiceBusFailureReason.MessageLockLost => MessageLockLost(fullyQualifiedNamespace, entityPath),
            _ => throw new InvalidOperationException($"Unexpected failure reason: {reason}"),
        };
    }

    public static ServiceBusException SessionCompleteMessageFailed(ServiceBusFailureReason reason, string fullyQualifiedNamespace, string entityPath, string sessionId)
    {
        return reason switch
        {
            ServiceBusFailureReason.SessionLockLost => SessionLockLost(fullyQualifiedNamespace, entityPath, sessionId),
            ServiceBusFailureReason.MessageLockLost => MessageLockLost(fullyQualifiedNamespace, entityPath),
            _ => throw new InvalidOperationException($"Unexpected failure reason: {reason}"),
        };
    }
    public static ServiceBusException SessionAbandonMessageFailed(ServiceBusFailureReason reason, string fullyQualifiedNamespace, string entityPath, string sessionId)
    {
        return reason switch
        {
            ServiceBusFailureReason.SessionLockLost => SessionLockLost(fullyQualifiedNamespace, entityPath, sessionId),
            _ => throw new InvalidOperationException($"Unexpected failure reason: {reason}"),
        };
    }


    private static ServiceBusException SessionLockLost(string fullyQualifiedNamespace, string entityPath, string sessionId)
    {
        return new($"Session lock lost for session '{sessionId}'.", ServiceBusFailureReason.SessionLockLost, EntityFullPath(fullyQualifiedNamespace, entityPath));
    }

    private static string EntityFullPath(string fullyQualifiedNamespace, string entityPath)
    {
        return $"{fullyQualifiedNamespace.TrimEnd('/')}/{entityPath}";
    }


}
