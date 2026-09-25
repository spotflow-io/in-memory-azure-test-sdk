namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class ContainerDeleteAfterHookContext(ContainerDeleteBeforeHookContext before) : ContainerAfterHookContext(before)
{
    public ContainerDeleteBeforeHookContext BeforeContext => before;
}
