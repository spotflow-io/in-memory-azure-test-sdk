using Azure.Storage.Blobs.Specialized;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;
using Spotflow.InMemory.Azure.Storage.FluentAssertions;

namespace Tests.Storage.Blobs;

[TestClass]
public class FluentAssertionsTests
{
    [TestMethod]
    public async Task ExistAsync_Should_Wait_For_Blob_To_Be_Created()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        var existTask = blob.Should().ExistAsync();

        existTask.IsCompleted.Should().BeFalse();

        await Task.Delay(100);

        existTask.IsCompleted.Should().BeFalse();

        await Task.Delay(100);

        blob.Upload(BinaryData.FromString("test-data"));

        await existTask;

        existTask.IsCompletedSuccessfully.Should().BeTrue();

    }
}
