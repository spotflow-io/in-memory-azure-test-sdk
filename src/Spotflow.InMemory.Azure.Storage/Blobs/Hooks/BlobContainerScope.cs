using Spotflow.InMemory.Azure.Storage.Hooks;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks;

public record BlobContainerScope(string StorageAccountName, string ContainerName) : StorageAccountScope(StorageAccountName);
