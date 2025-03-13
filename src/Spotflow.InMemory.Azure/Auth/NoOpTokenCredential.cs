using Azure.Core;

namespace Spotflow.InMemory.Azure.Auth;

public sealed class NoOpTokenCredential : TokenCredential
{
    public static NoOpTokenCredential Instance { get; } = new();

    public NoOpTokenCredential() { } // Keep constructor public to allow DI instantiation

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new AccessToken("no-op", default);
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new(new AccessToken("no-op", default));
    }
}
