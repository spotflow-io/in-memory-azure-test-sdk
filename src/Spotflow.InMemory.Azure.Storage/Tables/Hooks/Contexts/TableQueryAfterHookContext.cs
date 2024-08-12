using Azure.Data.Tables;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public class TableQueryAfterHookContext(TableQueryBeforeHookContext beforeContext) : TableAfterHookContext(beforeContext)
{
    public required IReadOnlyList<ITableEntity> Entities { get; init; }
    public TableQueryBeforeHookContext BeforeContext { get; } = beforeContext;
}
