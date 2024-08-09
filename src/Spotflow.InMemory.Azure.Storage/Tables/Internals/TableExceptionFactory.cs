using System.Runtime.CompilerServices;

using Azure;
using Azure.Data.Tables.Models;

using Spotflow.InMemory.Azure.Storage.Internals;

namespace Spotflow.InMemory.Azure.Storage.Tables.Internals;

internal static class TableExceptionFactory
{
    public static HttpRequestException TableServiceNotFound(string accountName, InMemoryStorageProvider provider)
    {
        return new($"Host '{InMemoryTableService.CreateServiceUriFromAccountName(accountName, provider)}' not found.");
    }

    public static RequestFailedException TableNotFound(string tableName, InMemoryTableService tableService)
    {
        return new(404, $"Table '{tableName}' not found in '{tableService}'.", TableErrorCode.ResourceNotFound.ToString(), null);
    }

    public static RequestFailedException TableAlreadyExists(string accountName, string tableName)
    {
        return new(409, $"Table '{tableName}' already exists in account '{accountName}'.", TableErrorCode.TableAlreadyExists.ToString(), null);

    }

    public static RequestFailedException EntityNotFound(string accountName, string tableName, string partitionKey, string rowKey)
    {
        return new(
            404,
            $"Entity '{partitionKey}/{rowKey}' not found in table '{tableName}' in account '{accountName}'.",
            TableErrorCode.ResourceNotFound.ToString(), // Not a bug. Azure Tables API returns this code.
            null);
    }

    public static RequestFailedException EntityAlreadyExists(string accountName, string tableName, string partitionKey, string rowKey)
    {
        return new(
            409,
            $"Entity '{partitionKey}/{rowKey}' already exist " +
            $"in table '{tableName}' in account '{accountName}'.",
            TableErrorCode.EntityAlreadyExists.ToString(),
            null);
    }

    public static RequestFailedException ConditionNotMet(string accountName, string tableName, string partitionKey, string rowKey, ConditionError error)
    {
        return new(
            412,
            $"Update condition '{error.ConditionType}' not satisfied " +
            $"for entity '{partitionKey}/{rowKey}' " +
            $"in table '{tableName}' in account '{accountName}': {error.Message}",
            TableErrorCode.UpdateConditionNotSatisfied.ToString(),
            null);
    }

    public static NotSupportedException MethodNotSupported([CallerMemberName] string? callerMemberName = null)
    {
        return new($"In-memory table storage client does not support method '{callerMemberName}'.");
    }

    public static NotSupportedException FeatureNotSupported(string featureName)
    {
        return new($"In-memory table storage client does not support feature '{featureName}'.");
    }

    public static RequestFailedException MultiplePartitionsInTransaction(string accountName, string tableName)
    {
        return new(
            400,
            $"Entities with different partition keys in a single transaction are not allowed " +
            $"for table '{tableName}' in account '{accountName}'.",
            TableErrorCode.CommandsInBatchActOnDifferentPartitions.ToString(),
            null);
    }

    public static RequestFailedException TooManyEntitiesInTransaction(string accountName, string tableName, int maxCount, int actualCount)
    {
        return new(
            400,
            $"At most {maxCount} entities can be present in the transaction. Found {actualCount} entities in transaction " +
            $"for table '{tableName}' in account '{accountName}'.",
            TableErrorCode.InvalidInput.ToString(),
            null);
    }

    public static RequestFailedException DuplicateEntityInTransaction(string accountName, string tableName, string partitionKey, string rowKey)
    {
        return new(
            400,
            $"Entity '{partitionKey}/{rowKey}' is duplicated in the transaction " +
            $"for table '{tableName}' in account '{accountName}'.",
            TableErrorCode.InvalidDuplicateRow.ToString(),
            null);
    }

    public static RequestFailedException ServiceIsBusy(string accountName)
    {
        return new(
            503,
            $"Table service in account '{accountName}' is busy.",
            "ServerBusy",
            null);
    }
}
