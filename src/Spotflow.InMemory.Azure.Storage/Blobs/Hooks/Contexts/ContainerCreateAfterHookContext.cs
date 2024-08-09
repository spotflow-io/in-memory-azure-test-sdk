using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class ContainerCreateAfterHookContext(ContainerCreateBeforeHookContext before) : ContainerAfterHookContext(before)
{
    public required BlobContainerInfo ContainerInfo { get; init; }
    public ContainerCreateBeforeHookContext BeforeContext => before;

}
