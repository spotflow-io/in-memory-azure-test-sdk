namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Internals;

internal interface ITableOperation
{
    string TableName { get; }
    TableOperations Operation { get; }
}
