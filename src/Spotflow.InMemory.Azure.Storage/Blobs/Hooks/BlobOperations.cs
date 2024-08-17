namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks;

[Flags]
public enum BlobOperations
{
    None = 0,
    Download = 1,
    Upload = 2,
    OpenRead = 4,
    OpenWrite = 8,
    All = Download | Upload | OpenRead | OpenWrite
}
