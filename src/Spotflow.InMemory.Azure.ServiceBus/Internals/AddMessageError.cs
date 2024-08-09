namespace Spotflow.InMemory.Azure.ServiceBus.Internals;

internal abstract class AddMessageError
{
    public abstract Exception GetClientException();

    public class SessionIdNotSetOnMessage(string fullyQualifiedNamespace, string entityPath) : AddMessageError
    {
        public override Exception GetClientException()
        {
            return ServiceBusExceptionFactory.SessionIdNotSetOnMessage(fullyQualifiedNamespace, entityPath);
        }
    }
}
