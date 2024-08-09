using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

public class BlobDownloadAfterHookContext(BlobDownloadBeforeHookContext before) : BlobAfterHookContext(before)
{
    public required BlobDownloadDetails BlobDownloadDetails { get; init; }
    public required BinaryData Content { get; init; }
    public BlobDownloadBeforeHookContext BeforeContext => before;
}

