namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public class TableCreateAfterHookContext(TableCreateBeforeHookContext before) : TableAfterHookContext(before)
{
    public TableCreateBeforeHookContext BeforeContext => before;
}
