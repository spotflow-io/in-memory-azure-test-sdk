using Azure;

namespace Spotflow.InMemory.Azure.Storage.Tables.Internals;

internal static class ETagExtensions
{
    public static bool IsEmpty(this ETag eTag) => eTag == new ETag(string.Empty) || eTag == default;

    public static void EnsureNotEmpty(this ETag eTag)
    {
        if (eTag.IsEmpty())
        {
            throw new InvalidOperationException("ETag cannot be empty.");
        }
    }
}
