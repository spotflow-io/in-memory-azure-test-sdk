namespace Spotflow.InMemory.Azure.ServiceBus.Internals;

internal record LockedSession(SessionStore Store, Guid SessionLockToken)
{
    public string SessionId => Store.SessionId;

    public string FullyQualifiedNamespace => Store.FullyQualifiedNamespace;

    public string EntityPath => Store.EntityPath;

}
