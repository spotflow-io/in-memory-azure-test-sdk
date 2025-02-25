using System.Runtime.CompilerServices;

using Azure;
using Azure.Storage.Blobs.Models;

using Spotflow.InMemory.Azure.Storage.Internals;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;

internal static class BlobExceptionFactory
{
    public static HttpRequestException BlobServiceNotFound(string accountName, InMemoryStorageProvider provider)
    {
        return new($"Host '{provider.GetAccount(accountName).BlobServiceUri}' not found.");
    }



    public static RequestFailedException ContainerNotFound(string containerName, InMemoryBlobService blobService)
    {
        return new(
            404,
            $"Container '{containerName}' not found in '{blobService}' account.",
            BlobErrorCode.ContainerNotFound.ToString(),
            null);
    }

    public static RequestFailedException ContainerAlreadyExists(string accountName, string containerName)
    {
        return new(
          412,
          $"Container '{containerName}' in account '{accountName}' already exist.",
          BlobErrorCode.ContainerNotFound.ToString(),
          null);
    }

    public static RequestFailedException BlobNotFound(string accountName, string blobContainerName, string blobName)
    {
        return new(404, $"Blob '{blobName}' not found in container '{blobContainerName}' in account '{accountName}'.", BlobErrorCode.BlobNotFound.ToString(), null);
    }

    public static RequestFailedException SourceBlobNotFound()
    {
        return new(404, "Source blob not found", BlobErrorCode.CannotVerifyCopySource.ToString(), null);
    }

    public static RequestFailedException SourceBlobContainerNotFound()
    {
        return new(404, "The specified container does not exist.", BlobErrorCode.CannotVerifyCopySource.ToString(), null);
    }

    public static RequestFailedException SourceBlobServiceNotFound()
    {
        return new(409, "Could not verify copy source", BlobErrorCode.CannotVerifyCopySource.ToString(), null);
    }

    public static RequestFailedException SourceBlobIfMatchFailed()
    {
        return new(412, "The condition specified using HTTP conditional header(s) is not met.", BlobErrorCode.CannotVerifyCopySource.ToString(), null);
    }

    public static RequestFailedException SourceBlobIfNoneMatchFailed()
    {
        // The actual response is different than in documentation https://learn.microsoft.com/en-us/rest/api/storageservices/put-blob-from-url?tabs=microsoft-entra-id#request-headers
        // But the status code 304 Not Modified does make sense.
        return new(304, "Service request failed.", BlobErrorCode.CannotVerifyCopySource.ToString(), null);
    }

    public static RequestFailedException BlockCountExceeded(string accountName, string blobContainerName, string blobName, int limit, int actualCount)
    {
        return new(
            409,
            $"Number of blocks for in a block list ({actualCount} exceeded the limit ({limit}) " +
            $"in blob '{blobName}' in container '{blobContainerName}' in account '{accountName}'.",
            BlobErrorCode.BlockCountExceedsLimit.ToString(),
            null
            );
    }

    public static RequestFailedException BlockNotFound(string accountName, string blobContainerName, string blobName, string blockId)
    {
        return new(
            400,
            $"Block '{blockId}' not found in blob '{blobName}' in container '{blobContainerName}' in account '{accountName}'.",
            BlobErrorCode.InvalidBlockList.ToString(),
            null
            );
    }

    public static RequestFailedException TooManyUncommittedBlocks(string accountName, string blobContainerName, string blobName, int limit, int actualCount)
    {
        return new(
            409,
            $"Number of uncommited blocks ({actualCount}) exceeded the limit ({limit}) " +
            $"in blob '{blobName}' in container '{blobContainerName}' in account '{accountName}'.",
            BlobErrorCode.BlockCountExceedsLimit.ToString(),
            null);
    }

    public static RequestFailedException BlockTooLarge(string accountName, string blobContainerName, string blobName, int limit, int actualSize)
    {
        return new(
            413,
            $"Size of block ({actualSize}) exceeded the limit ({limit}) " +
            $"in blob '{blobName}' in container '{blobContainerName}' in account '{accountName}'.",
            BlobErrorCode.RequestBodyTooLarge.ToString(),
            null);
    }

    public static RequestFailedException ConditionNotMet(string accountName, string blobContainerName, string blobName, ConditionError error)
    {
        return new(
            412,
            $"Condition {error.ConditionType} " +
            $"for blob '{blobName}' in container '{blobContainerName}' in account '{accountName} " +
            $"not met: {error.Message}'.",
            BlobErrorCode.ConditionNotMet.ToString(),
            null);
    }

    public static RequestFailedException ConditionNotMet(ConditionType conditionType, string accountName, string blobContainerName, string message)
    {
        return new(
            412,
            $"Condition {conditionType} " +
            $"for container '{blobContainerName}' in account '{accountName} " +
            $"not met: {message}'.",
            BlobErrorCode.ConditionNotMet.ToString(),
            null);
    }

    public static RequestFailedException InvalidRange()
    {
        return new(
            416,
            "The range specified is invalid for the current size of the resource.",
            BlobErrorCode.InvalidRange.ToString(),
            null);
    }

    public static RequestFailedException InvalidQueryParameterValue(
        string accountName,
        string blobContainerName,
        string blobName,
        string parameterName,
        string actualValue,
        string reason)
    {
        var ex = new RequestFailedException(
            400,
            $"Invalid query parameter '{parameterName}' = '{actualValue}' " +
            $"for blob '{blobName}' in container '{blobContainerName}' in account '{accountName} ",
            BlobErrorCode.InvalidQueryParameterValue.ToString(),
            null
            );

        ex.Data["QueryParameterName"] = parameterName;
        ex.Data["QueryParameterValue"] = actualValue;
        ex.Data["Reason"] = reason;

        return ex;
    }

    public static RequestFailedException InvalidContainerName(string accountName, string blobContainerName)
    {
        return new(
            400,
            $"Container name '{blobContainerName}' is invalid for account '{accountName}'.",
            BlobErrorCode.InvalidResourceName.ToString(),
            null);
    }

    public static RequestFailedException BlobAlreadyExists(string accountName, string containerName, string name)
    {
        return new(
            409,
            $"Blob '{name}' already exists in container '{containerName}' in account '{accountName}'.",
            BlobErrorCode.BlobAlreadyExists.ToString(),
            null);
    }

    public static NotSupportedException MethodNotSupported([CallerMemberName] string? callerMemberName = null)
    {
        return new($"In-memory blob storage client does not support method '{callerMemberName}'.");
    }

    public static NotSupportedException FeatureNotSupported(string featureName)
    {
        return new($"In-memory blob storage client does not support feature '{featureName}'.");
    }

    public static RequestFailedException ServiceIsBusy(string accountName)
    {
        return new(
            503,
            $"Blob service in account '{accountName}' is busy.",
            BlobErrorCode.ServerBusy.ToString(),
            null);
    }

    public static RequestFailedException AuthenticationFailedSignatureDidNotMatch(string storageAccountName)
    {
        var exception = new RequestFailedException(
            403,
            $"Server failed to authenticate the request. " +
            $"Make sure the value of Authorization header is formed correctly including the signature. " +
            $"Storage account '{storageAccountName}'.",
            BlobErrorCode.AuthenticationFailed.ToString(),
            null);

        exception.Data["AuthenticationErrorDetail"] = "Signature did not match.";

        return exception;
    }

    public static ArgumentNullException SharedKeyCredentialNotSet()
    {
        return new(paramName: "sharedKeyCredential");
    }
}
