namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;

internal static class BinaryDataExtensions
{
    public static int GetLenght(this BinaryData binaryData) => binaryData.ToMemory().Length;
}
