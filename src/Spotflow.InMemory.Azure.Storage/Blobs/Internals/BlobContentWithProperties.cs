using Azure.Storage.Blobs.Models;

namespace Spotflow.InMemory.Azure.Storage.Blobs.Internals;

internal record BlobContentWithProperties(BinaryData Content, BlobProperties Properties);
