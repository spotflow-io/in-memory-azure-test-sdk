using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;
using Spotflow.InMemory.Azure.Storage.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Internals;

internal record TableHookFilter : StorageHookFilter
{
    public TableHookFilter(StorageHookFilter filter) : base(filter) { }

    public string? TableName { get; private init; }
    public string? PartitionKey { get; private init; }
    public string? RowKey { get; private init; }
    public TableOperations TableOperations { get; private init; } = TableOperations.All;
    public EntityOperations EntityOperations { get; private init; } = EntityOperations.All;

    public override bool Covers(StorageHookContext context)
    {
        var result = base.Covers(context);

        if (context is ITableOperation table)
        {
            result &= TableName is null || TableName == table.TableName;
            result &= TableOperations.HasFlag(table.Operation);

            return result;
        }

        if (context is IEntityOperation entity)
        {
            result &= TableName is null || TableName == entity.TableName;
            result &= PartitionKey is null || PartitionKey == entity.PartitionKey;
            result &= RowKey is null || RowKey == entity.RowKey;
            result &= EntityOperations.HasFlag(entity.Operation);

            return result;
        }

        throw new InvalidOperationException($"Unexpected context: {context}");
    }

    internal TableHookFilter With(string? tableName = null, TableOperations? tableOperations = null, string? partitionKey = null, string? rowKey = null, EntityOperations? entityOperations = null)
    {
        return this with
        {
            TableName = tableName ?? TableName,
            PartitionKey = partitionKey ?? PartitionKey,
            RowKey = rowKey ?? RowKey,
            TableOperations = tableOperations ?? TableOperations,
            EntityOperations = entityOperations ?? EntityOperations
        };
    }
}



