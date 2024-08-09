using Azure.Data.Tables;

using Spotflow.InMemory.Azure.Storage.Hooks;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks;

public record TableScope(string StorageAccountName, string TableName) : StorageAccountScope(StorageAccountName)
{
    public EntityScope ForEntity<T>(T entity) where T : ITableEntity
    {
        return new(StorageAccountName, TableName, entity.PartitionKey, entity.RowKey);
    }
}
