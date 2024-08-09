using Spotflow.InMemory.Azure.Storage.Tables.Hooks.Internals;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;
public abstract class TableAfterHookContext(TableBeforeHookContext before) : TableServiceAfterHookContext(before), ITableOperation
{
    public string TableName => before.TableName;
    public TableOperations Operation => before.Operation;
}
