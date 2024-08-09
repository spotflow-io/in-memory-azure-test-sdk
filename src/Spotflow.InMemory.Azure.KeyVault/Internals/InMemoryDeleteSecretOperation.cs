using Azure;
using Azure.Core;
using Azure.Security.KeyVault.Secrets;

using Spotflow.InMemory.Azure.Internals;

namespace Spotflow.InMemory.Azure.KeyVault.Internals;

internal class InMemoryDeleteSecretOperation(InMemoryKeyVaultSecret secret) : DeleteSecretOperation
{
    public override string Id { get; } = Guid.NewGuid().ToString();

    public override DeletedSecret Value { get; } = SecretModelFactory.DeletedSecret(secret.ToClientProperties(), deletedOn: secret.DeletedOn);

    public override bool HasCompleted => true;

    public override bool HasValue => true;

    #region WaitForCompletion

    public override Response<DeletedSecret> WaitForCompletion(CancellationToken cancellationToken = default)
    {
        return InMemoryResponse.FromValue(Value, 200);
    }

    public override Response<DeletedSecret> WaitForCompletion(TimeSpan pollingInterval, CancellationToken cancellationToken)
    {
        return WaitForCompletion(cancellationToken);
    }

    public override Response<DeletedSecret> WaitForCompletion(DelayStrategy delayStrategy, CancellationToken cancellationToken)
    {
        return WaitForCompletion(cancellationToken);
    }

    public override async ValueTask<Response<DeletedSecret>> WaitForCompletionAsync(DelayStrategy delayStrategy, CancellationToken cancellationToken)
    {
        await Task.Yield();
        return WaitForCompletion(cancellationToken);
    }

    public override async ValueTask<Response<DeletedSecret>> WaitForCompletionAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return WaitForCompletion(cancellationToken);
    }

    public override async ValueTask<Response<DeletedSecret>> WaitForCompletionAsync(TimeSpan pollingInterval, CancellationToken cancellationToken)
    {
        await Task.Yield();
        return WaitForCompletion(cancellationToken);
    }

    public override Response WaitForCompletionResponse(CancellationToken cancellationToken = default)
    {
        return WaitForCompletion(cancellationToken).GetRawResponse();
    }

    public override Response WaitForCompletionResponse(TimeSpan pollingInterval, CancellationToken cancellationToken = default)
    {
        return WaitForCompletionResponse(cancellationToken);
    }

    public override Response WaitForCompletionResponse(DelayStrategy delayStrategy, CancellationToken cancellationToken = default)
    {
        return WaitForCompletionResponse(cancellationToken);
    }

    public override async ValueTask<Response> WaitForCompletionResponseAsync(DelayStrategy delayStrategy, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return WaitForCompletionResponse(cancellationToken);
    }

    public override async ValueTask<Response> WaitForCompletionResponseAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return WaitForCompletionResponse(cancellationToken);
    }

    public override async ValueTask<Response> WaitForCompletionResponseAsync(TimeSpan pollingInterval, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return WaitForCompletionResponse(cancellationToken);
    }

    #endregion

    #region Unsupported
    public override RehydrationToken? GetRehydrationToken() => throw KeyVaultExceptionFactory.MethodNotSupported();

    public override Response GetRawResponse() => throw KeyVaultExceptionFactory.MethodNotSupported();

    public override Response UpdateStatus(CancellationToken cancellationToken = default) => throw KeyVaultExceptionFactory.MethodNotSupported();

    public override ValueTask<Response> UpdateStatusAsync(CancellationToken cancellationToken = default) => throw KeyVaultExceptionFactory.MethodNotSupported();

    #endregion

}
