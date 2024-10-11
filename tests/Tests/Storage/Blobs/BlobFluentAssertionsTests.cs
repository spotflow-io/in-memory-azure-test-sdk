using Azure.Storage.Blobs.Specialized;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;
using Spotflow.InMemory.Azure.Storage.FluentAssertions;

namespace Tests.Storage.Blobs;

[TestClass]
public class BlobFluentAssertionsTests
{
    [TestMethod]
    public void Exist_With_Existing_Blob_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.Upload(BinaryData.FromString("test-data"));

        blob.Should().Exist();
    }

    [TestMethod]
    public void Exist_With_Missing_Blob_Should_Throw()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        var act = () => blob.Should().Exist();

        act.Should()
            .Throw<AssertFailedException>()
            .WithMessage("" +
                "Expected blob to exist but it does not." +
                "With Blob:https://*/test-container/test-blob");
    }


    [TestMethod]
    public async Task Exist_With_Wait_For_Eventually_Created_Blob_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        var existTask = Task.Run(() => blob.Should().Exist(waitTime: TimeSpan.FromSeconds(8)));

        existTask.IsCompleted.Should().BeFalse();

        await Task.Delay(100);

        existTask.IsCompleted.Should().BeFalse();

        await Task.Delay(100);

        blob.Upload(BinaryData.FromString("test-data"));

        await existTask;

        existTask.IsCompletedSuccessfully.Should().BeTrue();

    }

    [TestMethod]
    public async Task Exist_With_Wait_For_Missing_Blob_Should_Throw()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        var existTask = Task.Run(() => blob.Should().Exist(waitTime: TimeSpan.FromSeconds(1)));

        existTask.IsCompleted.Should().BeFalse();

        await Task.Delay(1000);

        var act = () => existTask;

        await act.Should()
            .ThrowAsync<AssertFailedException>()
            .WithMessage("" +
                "Expected blob to exist eventually but it does not exist event after 1.0 seconds.*" +
                "With Blob:*https://*/test-container/test-blob");
    }

    [TestMethod]
    public void MatchName_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.Should().MatchName("test-blob*");
    }

    [TestMethod]
    public void MatchName_Should_Throw()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        var act = () => blob.Should().MatchName("test-bloc*");

        act.Should()
            .Throw<AssertFailedException>()
            .WithMessage("" +
                "Expected name of the blob to match \"test-bloc*\", but \"test-blob\" does not." +
                "With Blob:https://*/test-container/test-blob");
    }


    [TestMethod]
    public void HaveSize_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.Upload(BinaryData.FromString("test-data"));

        blob.Should().HaveSize(9);
    }

    [TestMethod]
    public void HaveSize_Should_Throw()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.Upload(BinaryData.FromString("test-datax"));

        var act = () => blob.Should().HaveSize(9);

        act.Should()
            .Throw<AssertFailedException>()
            .WithMessage("" +
                "Expected size of the blob to be 9L, but found 10L (difference of 1)." +
                "With Blob:https://*/test-container/test-blob");
    }


    [TestMethod]
    public void BeEmpty_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.Upload(BinaryData.FromString(string.Empty));

        blob.Should().BeEmpty();
    }

    [TestMethod]
    public void BeEmpty_Should_Throw()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.Upload(BinaryData.FromString("test-data"));

        var act = () => blob.Should().BeEmpty();

        act.Should()
            .Throw<AssertFailedException>()
            .WithMessage("" +
            "Expected size of the blob to be 0L, but found 9L (difference of 9)." +
            "With Blob:*https://*/test-container/test-blob");
    }

    [TestMethod]
    public void HaveContent_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.Upload(BinaryData.FromString("test-data"));

        blob.Should().HaveContent("test-data");
    }

    [TestMethod]
    public void HaveContent_Should_Throw()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.Upload(BinaryData.FromString("test-data"));

        var act = () => blob.Should().HaveContent("test-data-x");

        act.Should()
            .Throw<AssertFailedException>()
            .WithMessage("" +
            "Expected content of the blob to be \"test-data-x\" with a length of 11, but \"test-data\" has a length of 9, differs near \"a\" (index 8)." +
            "With Blob:*https://*/test-container/test-blob");
    }

    [TestMethod]
    public void Block_Count_Assertions_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlockBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.StageBlock("dGVzdC1ibG9jay0x", BinaryData.FromString("test-data-1").ToStream());

        blob.Should().HaveNoCommittedBlocks();
        blob.Should().HaveUncommittedBlocks(1);

        blob.StageBlock("dGVzdC1ibG9jay0y", BinaryData.FromString("test-data-2").ToStream());

        blob.Should().HaveNoCommittedBlocks();
        blob.Should().HaveUncommittedBlocks(2);

        blob.CommitBlockList(["dGVzdC1ibG9jay0x"]);

        blob.Should().HaveCommittedBlocks(1);
        blob.Should().HaveNoUncommittedBlocks();
    }

    [TestMethod]
    public void Block_Count_Assertions_Should_Throw()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlockBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.StageBlock("dGVzdC1ibG9jay0x", BinaryData.FromString("test-data-1").ToStream());

        var actNoUncommittedBlocks = () => blob.Should().HaveNoUncommittedBlocks();
        actNoUncommittedBlocks.Should()
            .Throw<AssertFailedException>()
            .WithMessage("Expected number of uncommitted blocks in the blob '*/test-container/test-blob' to contain 0 item(s), but found 1:*");

        var actUncommittedBlocks = () => blob.Should().HaveUncommittedBlocks(2);
        actUncommittedBlocks.Should()
            .Throw<AssertFailedException>()
            .WithMessage("Expected number of uncommitted blocks in the blob '*/test-container/test-blob' to contain 2 item(s), but found 1:*");

        var actCommittedBlocks = () => blob.Should().HaveCommittedBlocks(1);
        actCommittedBlocks.Should()
            .Throw<AssertFailedException>()
            .WithMessage("Expected number of committed blocks in the blob '*/test-container/test-blob' to contain 1 item(s), but found 0:*");

        blob.CommitBlockList(["dGVzdC1ibG9jay0x"]);

        var actNoCommittedBlocks = () => blob.Should().HaveNoCommittedBlocks();
        actNoCommittedBlocks.Should()
            .Throw<AssertFailedException>()
            .WithMessage("Expected number of committed blocks in the blob '*/test-container/test-blob' to contain 0 item(s), but found 1:*");

    }

    [TestMethod]
    public void HaveCommittedBlockSizes_Should_Succeeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlockBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.StageBlock("dGVzdC1ibG9jay0x", BinaryData.FromString("test-data-a").ToStream());
        blob.StageBlock("dGVzdC1ibG9jay0y", BinaryData.FromString("test-data-ab").ToStream());
        blob.CommitBlockList(["dGVzdC1ibG9jay0x", "dGVzdC1ibG9jay0y"]);

        blob.Should().HaveCommittedBlocksWithSizes([11, 12]);
    }

    [TestMethod]
    public void HaveCommittedBlockSizes_With_Invalid_Number_Of_Blocks_Should_Throw()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlockBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.StageBlock("dGVzdC1ibG9jay0x", BinaryData.FromString("test-data-a").ToStream());
        blob.StageBlock("dGVzdC1ibG9jay0y", BinaryData.FromString("test-data-ab").ToStream());
        blob.CommitBlockList(["dGVzdC1ibG9jay0x", "dGVzdC1ibG9jay0y"]);

        var actOneLess = () => blob.Should().HaveCommittedBlocksWithSizes([11]);

        actOneLess.Should()
            .Throw<AssertFailedException>()
            .WithMessage("Expected number of committed blocks in the blob '*/test-container/test-blob' to contain 1 item(s), but found 2:*");

        var actOneMore = () => blob.Should().HaveCommittedBlocksWithSizes([11, 12, 13]);
        actOneMore.Should()
            .Throw<AssertFailedException>()
            .WithMessage("Expected number of committed blocks in the blob '*/test-container/test-blob' to contain 3 item(s), but found 2:*");

    }

    [TestMethod]
    public void HaveCommittedBlockSizes_With_Invalid_Sizes_Should_Throw()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlockBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.StageBlock("dGVzdC1ibG9jay0x", BinaryData.FromString("test-data-a").ToStream());
        blob.StageBlock("dGVzdC1ibG9jay0y", BinaryData.FromString("test-data-ab").ToStream());
        blob.CommitBlockList(["dGVzdC1ibG9jay0x", "dGVzdC1ibG9jay0y"]);

        var act = () => blob.Should().HaveCommittedBlocksWithSizes([24, 41]);

        act.Should()
            .Throw<AssertFailedException>()
            .WithMessage("" +
            "Expected block #0 size to be 24, but found 11 (difference of -13).\n" +
            "Expected block #1 size to be 41, but found 12 (difference of -29).*" +
            "With Blob:*https://*/test-container/test-blob");

    }


    [TestMethod]
    public void HaveCommittedBlock_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlockBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.StageBlock("dGVzdC1ibG9jay0x", BinaryData.FromString("test-data-a").ToStream());
        blob.CommitBlockList(["dGVzdC1ibG9jay0x"]);

        blob.Should().HaveCommittedBlock(0, block => block.Name.Should().Be("dGVzdC1ibG9jay0x"));

    }

    [TestMethod]
    public void HaveCommittedBlock_Should_Throw()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var blob = InMemoryBlockBlobClient.FromAccount(account, "test-container", "test-blob");

        blob.GetParentBlobContainerClient().Create();

        blob.StageBlock("dGVzdC1ibG9jay0x", BinaryData.FromString("test-data-a").ToStream());
        blob.CommitBlockList(["dGVzdC1ibG9jay0x"]);

        var act = () => blob.Should().HaveCommittedBlock(0, block => block.Name.Should().Be("XdGVzdC1ibG9jay0x"));

        act.Should()
            .Throw<AssertFailedException>()
            .WithMessage("" +
            "Expected block.Name to be \"XdGVzdC1ibG9jay0x\" with a length of 17, but \"dGVzdC1ibG9jay0x\" has a length of 16, differs near \"dGV\" (index 0).*" +
            "With Blob:*https://*/test-container/test-blob*" +
            "With Block:*#0");

    }


}
