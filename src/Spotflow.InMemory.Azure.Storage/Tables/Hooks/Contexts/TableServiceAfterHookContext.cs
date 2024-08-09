using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;

namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks.Contexts;

public abstract class TableServiceAfterHookContext(TableServiceBeforeHookContext before) : StorageAfterHookContext(before)
{
    public override TableServiceFaultsBuilder Faults() => new(this);

}
