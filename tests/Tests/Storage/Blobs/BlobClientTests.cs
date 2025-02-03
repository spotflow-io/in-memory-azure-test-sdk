using System.Text;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Spotflow.InMemory.Azure.Storage.FluentAssertions;

using Tests.Utils;

namespace Tests.Storage.Blobs;

[TestClass]
public class BlobClientTests
{
    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void Exists_For_Existing_Blob_Should_Return_True(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        Upload(blobClient, "Hello, World!");

        blobClient.Exists().Value.Should().BeTrue();

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void Exists_For_Missing_Blob_Should_Return_False(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        blobClient.Exists().Value.Should().BeFalse();

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void Upload_To_Non_Existing_Blob_With_ETag_Should_Fail(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        var options = new BlobUploadOptions
        {
            Conditions = new BlobRequestConditions
            {
                IfMatch = new ETag(Guid.NewGuid().ToString())
            }
        };

        var act = () => Upload(blobClient, "Hello, World!", options);

        act
            .Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 412)
            .Where(e => e.ErrorCode == "ConditionNotMet");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void Upload_To_Existing_With_IfNoneMatch_All_Should_Fail(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        var options = new BlobUploadOptions
        {
            Conditions = new BlobRequestConditions
            {
                IfNoneMatch = ETag.All
            }
        };

        var act = () => Upload(blobClient, "Hello, World!", options);

        act.Should().NotThrow();

        act
            .Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 409)
            .Where(e => e.ErrorCode == "BlobAlreadyExists");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void OpenWrite_Should_Create_Blob_With_Content(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        using (var stream = OpenWrite(blobClient, true))
        using (var streamWriter = new StreamWriter(stream))
        {
            streamWriter.Write("test-data1\n");
            streamWriter.Write("test-data2\n");
        }

        blobClient.DownloadContent().Value.Content.ToString().Should().Be("test-data1\ntest-data2\n");

        ShouldHaveBlocks(containerClient.GetBlockBlobClient(blobName), commited: 1, uncommited: 0);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void OpenWrite_And_Dispose_Immediately_Should_Create_Empty_Blob(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        using (var stream = OpenWrite(blobClient, true))
        using (var streamWriter = new StreamWriter(stream))
        {
            // Intentionally empty
        }

        blobClient.DownloadContent().Value.Content.ToString().Should().BeEmpty();

        ShouldHaveBlocks(containerClient.GetBlockBlobClient(blobName), commited: 0, uncommited: 0);

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void OpenWrite_And_Without_Dispose_Should_Create_Empty_Blob(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        using var stream = OpenWrite(blobClient, true);

        blobClient.DownloadContent().Value.Content.ToString().Should().BeEmpty();

        ShouldHaveBlocks(containerClient.GetBlockBlobClient(blobName), commited: 0, uncommited: 0);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void OpenWrite_Flush_And_Dispose_Should_Be_Idempotent(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        using (var stream = OpenWrite(blobClient, true))
        using (var streamWriter = new StreamWriter(stream))
        {
            streamWriter.Write("test-data1\n");
            streamWriter.Write("test-data2\n");

            stream.Flush();
            stream.Flush();
            stream.Flush();
        }

        blobClient.DownloadContent().Value.Content.ToString().Should().Be("test-data1\ntest-data2\n");

        ShouldHaveBlocks(containerClient.GetBlockBlobClient(blobName), commited: 1, uncommited: 0);
    }



    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void OpenWrite_Should_Set_Blob_Metadata(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        blobClient.Exists().Value.Should().BeFalse();

        var metadata = new Dictionary<string, string> { { "TestKey", "test-value" } };

        using var stream = OpenWrite(blobClient, true, metadata);

        stream.Dispose();

        blobClient.GetProperties().Value.Metadata.Should().Contain("TestKey", "test-value");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void OpenWrite_Without_Overwrite_Option_Should_Be_Unsupported(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        var act = () => OpenWrite(blobClient, false);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("BlockBlobClient.OpenWrite only supports overwriting");

    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void OpenWrite_Should_Return_Stream_Supporting_Position(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        using var stream = OpenWrite(blobClient, true);

        stream.Position.Should().Be(0);

        stream.WriteByte(42);

        stream.Position.Should().Be(1);

        var data = new byte[1 * 1024 * 1024];

        stream.Write(data);

        stream.Position.Should().Be((1 * 1024 * 1024) + 1);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void OpenWrite_For_Existing_Blob_With_Conditions_Should_Fail(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        Upload(blobClient, "Hello, World");

        var conditions = new BlobRequestConditions
        {
            IfNoneMatch = ETag.All
        };

        var act = () => OpenWrite(blobClient, true, conditions: conditions);

        act.Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 409)
            .Where(e => e.ErrorCode == "BlobAlreadyExists");

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void OpenWrite_Stream_Should_Create_Block_And_Commit_On_Each_Flush(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        using (var stream = OpenWrite(blobClient, true))
        {
            blobClient.Should().HaveNoCommittedBlocks();

            stream.WriteByte(1);
            stream.Flush();

            blobClient.Should().HaveCommittedBlocks(1);

            stream.WriteByte(2);
            stream.Flush();

            blobClient.Should().HaveCommittedBlocks(2);

            stream.WriteByte(2);

        }

        blobClient.Should().HaveCommittedBlocks(3);
    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void Download_Streaming_For_Non_Existing_Blob_Should_Fail(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        var act = () => blobClient.DownloadStreaming();

        act
            .Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 404)
            .Where(e => e.ErrorCode == "BlobNotFound");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void Delete_Existing_Blob_Should_Succeed(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        Upload(blobClient, "Hello, World!");

        var ifExistsResponse = blobClient.DeleteIfExists();

        ifExistsResponse.Value.Should().BeTrue();
        ifExistsResponse.GetRawResponse().Status.Should().Be(202);

        Upload(blobClient, "Hello, World!");

        var response = blobClient.Delete();

        response.Status.Should().Be(202);

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void Delete_Missings_Blob_Should_Fail(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        var ifExistsResponse = blobClient.DeleteIfExists();

        ifExistsResponse.Value.Should().BeFalse();
        ifExistsResponse.GetRawResponse().Should().BeNull();

        var act = () => blobClient.Delete();

        act
            .Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 404)
            .Where(e => e.ErrorCode == "BlobNotFound");

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void GetProperties_For_Existing_Blob_Should_Succeed(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        Upload(blobClient, "Hello, World!");

        var properties = blobClient.GetProperties().Value;

        properties.ContentLength.Should().Be(13);
        properties.CreatedOn.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromHours(1));
        properties.LastModified.Should().Be(properties.CreatedOn);
        properties.BlobType.Should().Be(BlobType.Block);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void GenerateSasUri_For_Existing_Blob_Should_Succeed(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        var sasUri = blobClient.GenerateSasUri(Azure.Storage.Sas.BlobSasPermissions.Read, new DateTimeOffset(2025, 01, 03, 17, 46, 00, TimeSpan.Zero));

        var expectedUri = $"{containerClient.Uri}/{blobName}?sv=2024-05-04&se=2025-01-03T17%3a46%3a00.000Z&sr=b&sp=r&sig=xxx";

        sasUri.ToString().Should().StartWith(expectedUri.ToString());
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void OpenRead_For_Existing_Blob_Should_Succeed(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        Upload(blobClient, "Hello, World!");

        using var stream = blobClient.OpenRead();

        new StreamReader(stream).ReadToEnd().Should().Be("Hello, World!");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void OpenRead_For_Existing_Blob_With_Position_Should_Succeed(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        Upload(blobClient, "Lorem ipsum dolor sit amet, consectetur adipiscing elit");

        using var stream = blobClient.OpenRead(position: 3);

        new StreamReader(stream, bufferSize: 5).ReadToEnd().Should().Be("em ipsum dolor sit amet, consectetur adipiscing elit");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic)]
    [DataRow(BlobClientType.Block)]
    public void OpenRead_For_Missing_Blob_Should_Fail(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        var act = () => blobClient.OpenRead();

        act.Should()
            .Throw<RequestFailedException>()
            .Where(e => e.Status == 404)
            .Where(e => e.ErrorCode == "BlobNotFound");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(BlobClientType.Generic, true)]
    [DataRow(BlobClientType.Generic, false)]
    [DataRow(BlobClientType.Block, true)]
    [DataRow(BlobClientType.Block, false)]
    public void OpenRead_For_Blob_Changed_In_Progress_Should_Respect_AllowBlobModifications_Flag(BlobClientType clientType, bool allowBlobModifications)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        var random = new Random(42);
        var data = new byte[1024];

        random.NextBytes(data);
        Upload(blobClient, data);
        var eTag1 = blobClient.GetProperties().Value.ETag;

        var clientBufferSize = 128;

        var options = new BlobOpenReadOptions(allowModifications: allowBlobModifications)
        {
            BufferSize = clientBufferSize
        };

        using var stream = blobClient.OpenRead(options);

        var dataBuffer = new byte[clientBufferSize * 10];

        stream.Read(dataBuffer, 0, clientBufferSize + 1).Should().Be(clientBufferSize);

        random.NextBytes(data);
        Upload(blobClient, data);
        var eTag2 = blobClient.GetProperties().Value.ETag;

        eTag1.Should().NotBe(eTag2);

        var act = () => stream.Read(dataBuffer, 0, 1);

        if (allowBlobModifications)
        {
            act.Should().NotThrow().Which.Should().Be(1);
        }
        else
        {
            act.Should()
                .Throw<RequestFailedException>()
                .Where(e => e.Status == 412)
                .Where(e => e.ErrorCode == "ConditionNotMet");
        }
    }

    public enum BlobClientType
    {
        Generic,
        Block
    }

    private static void Upload(BlobBaseClient blobClient, string content, BlobUploadOptions? options = null)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        Upload(blobClient, bytes, options);
    }

    private static void Upload(BlobBaseClient blobClient, byte[] content, BlobUploadOptions? options = null)
    {
        var stream = new MemoryStream(content);

        if (blobClient is BlobClient genericClient)
        {
            genericClient.Upload(stream, options: options);
        }
        else if (blobClient is BlockBlobClient blockClient)
        {
            blockClient.Upload(stream, options: options);
        }
        else
        {
            throw new InvalidOperationException();
        }
    }

    private static Stream OpenWrite(BlobBaseClient blobClient, bool overwrite, IDictionary<string, string>? metadata = null, BlobRequestConditions? conditions = null)
    {
        if (blobClient is BlobClient genericClient)
        {
            BlobOpenWriteOptions? options = null;

            if (metadata is not null)
            {
                options ??= new();
                options.Metadata = metadata;
            }

            if (conditions is not null)
            {
                options ??= new();
                options.OpenConditions = conditions;
            }

            return genericClient.OpenWrite(overwrite, options);
        }

        if (blobClient is BlockBlobClient blockClient)
        {
            BlockBlobOpenWriteOptions? options = null;

            if (metadata is not null)
            {
                options ??= new();
                options.Metadata = metadata;
            }

            if (conditions is not null)
            {
                options ??= new();
                options.OpenConditions = conditions;
            }

            return blockClient.OpenWrite(overwrite, options);
        }

        throw new InvalidOperationException("Unexpected client type.");

    }

    private static void ShouldHaveBlocks(BlockBlobClient client, int? commited, int? uncommited)
    {
        var blockList = client.GetBlockList().Value;

        if (commited.HasValue)
        {
            blockList.CommittedBlocks.Should().HaveCount(commited.Value);
        }

        if (uncommited.HasValue)
        {
            blockList.UncommittedBlocks.Should().HaveCount(uncommited.Value);
        }
    }

}
