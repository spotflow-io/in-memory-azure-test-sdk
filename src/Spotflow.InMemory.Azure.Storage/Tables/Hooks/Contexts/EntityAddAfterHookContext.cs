using Azure.Data.Tables;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public class EntityAddAfterHookContext(EntityAddBeforeHookContext beforeContext) : EntityAfterHookContext(beforeContext)
{
    public required ITableEntity Entity { get; init; }
    public EntityAddBeforeHookContext BeforeContext { get; } = beforeContext;
}

