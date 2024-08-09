namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Internals;

internal interface IEntityOperation
{
    string TableName { get; }
    string PartitionKey { get; }
    string RowKey { get; }
    EntityOperations Operation { get; }
}
