using Azure.Storage.Blobs.Specialized;

namespace Spotflow.InMemory.Azure.Storage.FluentAssertions;

public static class ShouldExtensions
{
    public static BlobBaseClientAssertions Should(this BlobBaseClient client)
    {
        return new(client);
    }
}
