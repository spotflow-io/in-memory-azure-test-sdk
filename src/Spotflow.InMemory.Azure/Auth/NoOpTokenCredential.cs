using Azure.Core;

namespace Spotflow.InMemory.Azure.Auth;

public sealed class NoOpTokenCredential : TokenCredential
{
    public static NoOpTokenCredential Instance { get; } = new();

    private NoOpTokenCredential() { }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new AccessToken("no-op", default);
    }

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
    {
        return new(new AccessToken("no-op", default));
    }
}
