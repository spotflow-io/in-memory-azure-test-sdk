using Azure.Data.Tables;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public class EntityUpsertAfterHookContext(EntityUpsertBeforeHookContext before) : EntityAfterHookContext(before)
{
    public required ITableEntity Entity { get; init; }
    public EntityUpsertBeforeHookContext BeforeContext => before;
}
