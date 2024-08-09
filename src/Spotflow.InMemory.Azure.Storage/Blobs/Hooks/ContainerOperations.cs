namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks;

[Flags]
public enum ContainerOperations
{
    None = 0,
    Create = 1,
    All = Create
}
