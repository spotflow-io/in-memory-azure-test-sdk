using Spotflow.InMemory.Azure.Storage.Hooks;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks;

public record BlobScope(string StorageAccountName, string ContainerName, string BlobName) : StorageAccountScope(StorageAccountName);
