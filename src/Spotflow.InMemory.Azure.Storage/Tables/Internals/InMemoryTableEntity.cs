using System.ComponentModel.DataAnnotations;
using System.Reflection;

using Azure;
using Azure.Data.Tables;

namespace Spotflow.InMemory.Azure.Storage.Tables.Internals;

internal class InMemoryTableEntity
{
    private readonly TableEntity _inner;

    public class EdmType
    {
        [Key]
        public required string PartitionKey { get; init; }

        [Key]
        public required string RowKey { get; init; }

        public Dictionary<string, object>? CustomFields { get; init; }
    }

    private InMemoryTableEntity(TableEntity inner)
    {
        _inner = inner;
    }

    public string PartitionKey => _inner.PartitionKey;
    public string RowKey => _inner.RowKey;
    public DateTimeOffset? Timestamp => _inner.Timestamp;
    public ETag ETag => _inner.ETag;

    public object? GetPropertyValueOrNull(string propertyName)
    {
        if (_inner.TryGetValue(propertyName, out var value))
        {
            return value;
        }

        return null;
    }

    public (string PartitionKey, string RowKey) Key => (PartitionKey, RowKey);

    public InMemoryTableEntity Update<T>(T other, TableUpdateMode mode, TimeProvider timeProvider) where T : ITableEntity
    {
        if (other.PartitionKey != PartitionKey || other.RowKey != RowKey)
        {
            throw new InvalidOperationException("Cannot merge entities with different keys.");
        }

        TableEntity newInner;

        if (mode is TableUpdateMode.Replace)
        {
            newInner = new();
        }
        else if (mode is TableUpdateMode.Merge)
        {
            newInner = new();

            foreach (var (key, value) in _inner)
            {
                newInner.Add(key, value);
            }
        }
        else
        {
            throw new InvalidOperationException($"Unexpected update mode: {mode}.");
        }

        foreach (var (key, value) in FromAzureTableEntity(other)._inner)
        {
            newInner.Add(key, value);
        }

        newInner.ETag = GenerateETag();
        newInner.Timestamp = timeProvider.GetUtcNow();

        return new(newInner);
    }

    public InMemoryTableEntity WithNewETagAndTimestamp(DateTimeOffset timestamp)
    {
        var newInner = new TableEntity();

        foreach (var (key, value) in _inner)
        {
            newInner.Add(key, value);
        }

        newInner.ETag = new ETag(Guid.NewGuid().ToString());
        newInner.Timestamp = timestamp;

        return new(newInner);
    }




    public T ToAzureTableEntity<T>() where T : ITableEntity
    {
        if (typeof(T) == typeof(TableEntity))
        {
            return (T) (object) _inner;
        }

        var entity = Activator.CreateInstance<T>();

        foreach (var (key, value) in _inner)
        {
            var property = entity.GetType().GetProperty(key, BindingFlags.Instance | BindingFlags.Public);

            if (property?.SetMethod is not null)
            {
                property.SetValue(entity, value);
            }
        }

        entity.PartitionKey = PartitionKey;
        entity.RowKey = RowKey;
        entity.ETag = ETag;
        entity.Timestamp = Timestamp;

        return entity;
    }

    public static InMemoryTableEntity CreateNew<T>(T azureTableEntity, TimeProvider timeProvider) where T : ITableEntity
    {
        var entity = FromAzureTableEntity(azureTableEntity);

        entity._inner.ETag = GenerateETag();
        entity._inner.Timestamp = timeProvider.GetUtcNow();

        return entity;
    }

    private static ETag GenerateETag() => new($@"W/""{Guid.NewGuid()}""");

    private static InMemoryTableEntity FromAzureTableEntity<T>(T azureTableEntity) where T : ITableEntity
    {
        TableEntity inner;

        if (azureTableEntity is TableEntity tableEntity)
        {
            inner = tableEntity;
        }
        else
        {
            inner = new();

            foreach (var property in azureTableEntity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                var value = property.GetValue(azureTableEntity);
                inner.Add(property.Name, value);
            }

        }

        inner.PartitionKey = azureTableEntity.PartitionKey;
        inner.RowKey = azureTableEntity.RowKey;

        return new InMemoryTableEntity(inner);

    }
}

