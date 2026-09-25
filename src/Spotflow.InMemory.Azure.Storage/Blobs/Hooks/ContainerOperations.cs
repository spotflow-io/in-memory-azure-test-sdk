namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks;

[Flags]
public enum ContainerOperations
{
    None = 0,
    Create = 1,
    Delete = 2,
    All = Create | Delete
}
