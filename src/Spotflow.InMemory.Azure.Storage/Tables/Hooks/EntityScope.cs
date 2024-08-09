namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks;

public record EntityScope(string StorageAccountName, string TableName, string PartitionKey, string RowKey) : TableScope(StorageAccountName, TableName);
