using System.Text;

using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;
using Spotflow.InMemory.Azure.Storage.Resources;

using Tests.Utils;

namespace Tests.Storage.Blobs;

[TestClass]
public class BlobClientTests_BlockBlobClient
{
    [TestMethod]
    public void Constructor_With_Connection_String_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var connectionString = account.GetConnectionString();

        var client = new InMemoryBlockBlobClient(connectionString, "test-container", "test-blob", provider);

        AssertClientProperties(client, "test-container", "test-blob", account, canGenerateSasUri: true);
    }

    [TestMethod]
    public void Constructor_With_Uri_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var client = new InMemoryBlockBlobClient(account.GetBlobSasUri("test-container", "test-blob"), provider);

        AssertClientProperties(client, "test-container", "test-blob", account);
    }

    [TestMethod]
    public void Constructor_With_Uri_Without_Container_Should_Fail()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var act = () => new InMemoryBlockBlobClient(account.BlobServiceUri, provider);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Blob container name must be specified when creating a blob client.");
    }

    [TestMethod]
    public void Constructor_With_Uri_Without_Blob_Should_Fail()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var act = () => new InMemoryBlockBlobClient(account.GetBlobContainerSasUri("test"), provider);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Blob name must be specified when creating a blob client.");
    }

    [TestMethod]
    public void Construct_From_Account_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var client = InMemoryBlockBlobClient.FromAccount(account, "test-container", "test-blob");

        AssertClientProperties(client, "test-container", "test-blob", account);
    }

    [TestMethod]
    public void Construct_From_Account_With_Connection_String_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var client = InMemoryBlockBlobClient.FromAccount(account, "test-container", "test-blob", useConnectionString: true);

        AssertClientProperties(client, "test-container", "test-blob", account, canGenerateSasUri: true);
    }

    private static void AssertClientProperties(
        InMemoryBlockBlobClient client,
        string expectedContainerName,
        string expectedBlobName,
        InMemoryStorageAccount account,
        bool canGenerateSasUri = false)
    {
        var expectedUri = new Uri(account.BlobServiceUri, $"{expectedContainerName}/{expectedBlobName}");

        client.Uri.Should().Be(expectedUri);
        client.AccountName.Should().Be(account.Name);
        client.BlobContainerName.Should().Be(expectedContainerName);
        client.Name.Should().Be(expectedBlobName);
        client.CanGenerateSasUri.Should().Be(canGenerateSasUri);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void StageBlock_With_Invalid_Id_Should_Be_Rejected()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = "test-block-id";

        var act = () => blobClient.StageBlock(blockId, BinaryData.FromString("test-data").ToStream());

        act.Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 400)
            .Where(e => e.ErrorCode == "InvalidQueryParameterValue")
            .Where(e => AssertHasInvalidBlockIdData(e, blockId));

    }

    private static bool AssertHasInvalidBlockIdData(RequestFailedException ex, string actualBlockId)
    {
        ex.Data["QueryParameterName"].Should().Be("blockid");
        ex.Data["QueryParameterValue"].Should().Be(actualBlockId);
        ex.Data["Reason"].Should().Be("Not a valid base64 string.");

        return true;
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Exists_For_Blob_With_Uncommited_Blocks_Only_Should_Be_False()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-block-id"));

        blobClient.StageBlock(blockId, BinaryData.FromString("test-data").ToStream());

        blobClient.Exists().Value.Should().BeFalse();
    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void StageBlock_Without_Commit_Should_Not_Cause_Overwrite()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("test-data-1"));

        blobClient.Upload(content);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-block-id"));

        blobClient.StageBlock(blockId, BinaryData.FromString("test-data-2").ToStream());

        blobClient.DownloadContent().Value.Content.ToString().Should().Be("test-data-1");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Stage_Block_And_Commit_Should_Create_Blob_With_Commited_Blocks()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-block-id"));

        blobClient
            .StageBlock(blockId, BinaryData.FromString("test-data").ToStream())
            .GetRawResponse()
            .Status
            .Should()
            .Be(201);

        blobClient.CommitBlockList([blockId]);

        var blockList = blobClient.GetBlockList().Value;

        blockList.CommittedBlocks.Should().ContainSingle(block => block.Name == blockId);
        blockList.UncommittedBlocks.Should().BeEmpty();

        blobClient.DownloadContent().Value.Content.ToString().Should().Be("test-data");

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void StageBlockFromUri_From_Another_Container()
    {
        var serviceClient = ImplementationProvider.GetBlobServiceClient();

        var sourceContainerClient = serviceClient.GetBlobContainerClient("source");
        sourceContainerClient.CreateIfNotExists();

        var containerClient = serviceClient.GetBlobContainerClient("target");
        containerClient.CreateIfNotExists();

        var sourceBlobClient = sourceContainerClient.GetBlobClient("source-blob");
        sourceBlobClient.Upload(BinaryData.FromString("test-data"), overwrite: true);

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-block-id"));

        blobClient
            .StageBlockFromUri(sourceBlobClient.Uri, blockId)
            .GetRawResponse()
            .Status
            .Should()
            .Be(201);

        blobClient.CommitBlockList([blockId]);

        blobClient.DownloadContent().Value.Content.ToString().Should().Be("test-data");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void StageBlockFromUri_When_Source_Blob_Does_Not_Exist_Should_Fail()
    {
        var serviceClient = ImplementationProvider.GetBlobServiceClient();

        var sourceContainerClient = serviceClient.GetBlobContainerClient("source");
        sourceContainerClient.CreateIfNotExists();

        var containerClient = serviceClient.GetBlobContainerClient("target");
        containerClient.CreateIfNotExists();

        var sourceBlob = new Uri(string.Join("/", sourceContainerClient.Uri.ToString(), "missing-blob"));

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-block-id"));

        var act = () => blobClient.StageBlockFromUri(sourceBlob, blockId);

        act
            .Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 404)
            .Where(e => e.ErrorCode == "CannotVerifyCopySource");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void StageBlockFromUri_Source_IfNoneMatch_Condition_Not_Met()
    {
        var serviceClient = ImplementationProvider.GetBlobServiceClient();

        var sourceContainerClient = serviceClient.GetBlobContainerClient("source");
        sourceContainerClient.CreateIfNotExists();

        var sourceBlobClient = sourceContainerClient.GetBlobClient(Guid.NewGuid().ToString());
        sourceBlobClient.Upload(BinaryData.FromString("test-data"), overwrite: true);

        var containerClient = serviceClient.GetBlobContainerClient("target");
        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-block-id"));

        var act = () => blobClient.StageBlockFromUri(sourceBlobClient.Uri, blockId, new StageBlockFromUriOptions()
        {
            SourceConditions = new RequestConditions()
            {
                IfNoneMatch = sourceBlobClient.GetProperties().Value.ETag
            }
        });

        act.Should().Throw<RequestFailedException>()
            .Where(e => e.Status == 304)
            .Where(e => e.ErrorCode == "CannotVerifyCopySource");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void CommitBlockList_With_Existing_Blocks_Should_Create_Blob_And_Clear_Uncommited_Blocks()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-block-id"));

        blobClient.StageBlock(blockId, BinaryData.FromString("test-data").ToStream());

        blobClient.GetBlockList().Value.UncommittedBlocks.Should().ContainSingle(b => b.Name == blockId);

        blobClient.CommitBlockList([]);

        var blockList = blobClient.GetBlockList().Value;

        blockList.CommittedBlocks.Should().BeEmpty();
        blockList.UncommittedBlocks.Should().BeEmpty();

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void CommitBlockList_With_No_Blocks_Should_Create_Empty_Blob()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        blobClient.CommitBlockList([]);

        blobClient.Exists().Value.Should().BeTrue();

        var blockList = blobClient.GetBlockList().Value;

        blockList.CommittedBlocks.Should().BeEmpty();
        blockList.UncommittedBlocks.Should().BeEmpty();

        blobClient.DownloadContent().Value.Content.ToMemory().Length.Should().Be(0);

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void CommitBlockList_Should_Set_Properties_And_Headers()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-block-id"));

        blobClient.StageBlock(blockId, BinaryData.FromString("test-data").ToStream());

        blobClient.CommitBlockList(
            [blockId],
            new BlobHttpHeaders
            {
                ContentType = "test/test",
                ContentEncoding = "gzip"
            },
            new Dictionary<string, string> { { "metadata1", "42" } }
            );


        var blockList = blobClient.GetBlockList().Value;

        blockList.CommittedBlocks.Should().ContainSingle(block => block.Name == blockId);
        blockList.UncommittedBlocks.Should().BeEmpty();

        var properties = blobClient.GetProperties().Value;

        properties.Metadata.Should().Contain("metadata1", "42");
        properties.ContentType.Should().Be("test/test");
        properties.ContentEncoding.Should().Be("gzip");

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void CommitBlockList_With_Blocks_To_Non_Existent_Blob_Should_Fail()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));

        var act = () => blobClient.CommitBlockList([blockId]);

        act
            .Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 400)
            .Where(e => e.ErrorCode == "InvalidBlockList");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void CommitBlockList_With_Missing_Block_To_Existing_Blob_Should_Fail()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId1 = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));
        var blockId2 = Convert.ToBase64String(Encoding.UTF8.GetBytes("2"));

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));

        blobClient.StageBlock(blockId1, content);

        var act = () => blobClient.CommitBlockList([blockId1, blockId2]);

        act
            .Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 400)
            .Where(e => e.ErrorCode == "InvalidBlockList");
    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void CommitBlockList_To_Existing_Blob_With_IfNoneMatch_All_Should_Fail()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));

        blobClient.StageBlock(blockId, content);

        var act = () => blobClient.CommitBlockList([blockId], options: new() { Conditions = new() { IfNoneMatch = ETag.All } });

        act.Should().NotThrow();

        act
            .Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 409)
            .Where(e => e.ErrorCode == "BlobAlreadyExists");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void GetBlockList_For_Blob_With_Uncommited_Blocks_Only_Should_Return_Uncommited_Blocks()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-block-id"));

        blobClient.StageBlock(blockId, BinaryData.FromString("test-data").ToStream());

        var blockList = blobClient.GetBlockList().Value;

        blockList.CommittedBlocks.Should().BeEmpty();
        blockList.UncommittedBlocks.Should().ContainSingle(b => b.Name == blockId);

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void GetBlockList_For_Deleted_Blob_Should_Fail()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));

        blobClient.Upload(content);

        blobClient.Exists().Value.Should().BeTrue();

        blobClient.Delete();

        var act = () => blobClient.GetBlockList();

        act
            .Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 404)
            .Where(e => e.ErrorCode == "BlobNotFound");

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void DownloadContent_For_Blob_With_Uncommited_Blocks_Only_Should_Fail()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("test-block-id"));

        blobClient.StageBlock(blockId, BinaryData.FromString("test-data").ToStream());

        var act = () => blobClient.DownloadContent();

        act
            .Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 404)
            .Where(e => e.ErrorCode == "BlobNotFound");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void GetProperties_From_Blob_With_Uncommited_Blocks_Only_Should_Fail()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlockBlobClient(blobName);

        var blockId = Convert.ToBase64String(Encoding.UTF8.GetBytes("1"));

        using var content = new MemoryStream(Encoding.UTF8.GetBytes("Hello, World!"));

        blobClient.StageBlock(blockId, content);

        blobClient.Exists().Value.Should().BeFalse();

        var act = () => blobClient.GetProperties();

        act
            .Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 404)
            .Where(e => e.ErrorCode == "BlobNotFound");
    }

    [TestMethod]
    public void GenerateSasUri_With_Manually_Created_Client_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount("testaccount");

        var connectionString = account.GetConnectionString();

        var client = new InMemoryBlockBlobClient(connectionString, "test-container", "test-blob", provider);

        var sasUri = client.GenerateSasUri(BlobSasPermissions.Read, new DateTimeOffset(2025, 01, 03, 17, 46, 00, TimeSpan.Zero));

        var expectedUri = $"https://testaccount.blob.storage.in-memory.example.com/test-container/test-blob?sv=2024-05-04&se=2025-01-03T17%3A46%3A00Z&sr=b&sp=r&sig=*";

        sasUri.ToString().Should().Match(expectedUri);
    }

    [TestMethod]
    public void GenerateSasUri_With_Client_Created_From_Container_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount("testaccount");

        var container = InMemoryBlobContainerClient.FromAccount(account, "test-container", useConnectionString: true);

        var client = container.GetBlockBlobClient("test-blob");

        var sasUri = client.GenerateSasUri(BlobSasPermissions.Read, new DateTimeOffset(2025, 01, 03, 17, 46, 00, TimeSpan.Zero));

        var expectedUri = $"https://testaccount.blob.storage.in-memory.example.com/test-container/test-blob?sv=2024-05-04&se=2025-01-03T17%3A46%3A00Z&sr=b&sp=r&sig=*";

        sasUri.ToString().Should().Match(expectedUri);
    }
}
