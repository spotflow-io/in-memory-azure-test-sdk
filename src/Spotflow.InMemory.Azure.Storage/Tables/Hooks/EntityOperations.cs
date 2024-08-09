namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks;

[Flags]
public enum EntityOperations
{
    None = 0,
    Add = 1,
    Upsert = 2,
    All = Add | Upsert
}
