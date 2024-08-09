# Hooks

Hooks are a way to extend the functionality of the core library. Hooks enable to inject some custom behavior before and after client operations.

User can register before and after hooks by calling `AddHook` method on resource providers such as [`InMemoryStorageProvider`](./storage.md). When adding a hook, user targets one or more client operations to which the hook should be applied and a function that should be executed before or after the operation. This function receives a context which contains information about the currently executed operation.

```csharp
var provider = new InMemoryStorageProvider();

// Register hook

// Before all storage operations:

provider.AddHook(hook => hook.Before(context => { ... }));

// Before creating any container in any storage account:

provider.AddHook(hook => hook.ForBlobService().ForContainerOperations().BeforeCreate(context => { ... }));

// Before creating specific container in any storage account:

provider.AddHook(hook => hook
  .ForBlobService()
  .ForContainerOperations(containerName: "container")
  .BeforeCreate(context => { ... }));

// After creating any container or uploading any blob within a specific storage account:

provider.AddHook(hook => hook
  .ForBlobService(storageAccountName: "account")
  .ForContainerOperations()
  .After(context => { ... }), containerOperations: ContainerOperations.Create, blobOperations: BlobOperations.Upload);
```

## Contexts

The contexts passed to the hook functions are organized into an inheritance hierarchy. For example, after hook for blob upload receives `BlobUploadAfterHookContext` type which inherits from the `BlobAfterHookContext` ->  `BlobServiceAfterHookContext` -> `StorageAfterHookContext` hierarchy.

This enabled the hook functions to access specific information about the currently executed operation even if the hook targets a more general group of operations (e.g. all storage operations). This can be done e.g via pattern matching:

```csharp
provider.AddHook(hook => hook.Before(hookFunc));

static Task hookFunc(StorageAfterHookContext context)
{
  if(context is BlobUploadAfterHookContext blobUpload)
  {
    ...
  }
  else
  {
    ...
  }
}
```
