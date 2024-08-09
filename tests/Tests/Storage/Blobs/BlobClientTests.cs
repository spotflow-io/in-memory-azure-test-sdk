using System.Text;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

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

        var blobClient = GetClient(containerClient, blobName, clientType);

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

        var blobClient = GetClient(containerClient, blobName, clientType);

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

        var blobClient = GetClient(containerClient, blobName, clientType);

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

        var blobClient = GetClient(containerClient, blobName, clientType);

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
    public void OpenWrite_And_Dispose_Should_Create_Blob(BlobClientType clientType)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = GetClient(containerClient, blobName, clientType);

        using (var stream = OpenWrite(blobClient, true))
        using (var streamWriter = new StreamWriter(stream))
        {

            blobClient.DownloadContent().Value.Content.ToString().Should().BeEmpty();

            streamWriter.Write("test-data1\n");
            streamWriter.Write("test-data2\n");
        }

        blobClient.DownloadContent().Value.Content.ToString().Should().Be("test-data1\ntest-data2\n");

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

        var blobClient = GetClient(containerClient, blobName, clientType);

        var act = () => OpenWrite(blobClient, false);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("BlockBlobClient.OpenWrite only supports overwriting");

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

        var blobClient = GetClient(containerClient, blobName, clientType);

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

        var blobClient = GetClient(containerClient, blobName, clientType);

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

        var blobClient = GetClient(containerClient, blobName, clientType);

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

        var blobClient = GetClient(containerClient, blobName, clientType);

        Upload(blobClient, "Hello, World!");

        var properties = blobClient.GetProperties().Value;

        properties.BlobType.Should().Be(BlobType.Block);
    }

    public enum BlobClientType
    {
        Generic,
        Block
    }

    private static BlobBaseClient GetClient(BlobContainerClient containerClient, string blobName, BlobClientType type)
    {
        return type switch
        {
            BlobClientType.Generic => containerClient.GetBlobClient(blobName),
            BlobClientType.Block => containerClient.GetBlockBlobClient(blobName),
        };
    }

    private static void Upload(BlobBaseClient blobClient, string content, BlobUploadOptions? options = null)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

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

    private static Stream OpenWrite(BlobBaseClient blobClient, bool overwrite)
    {
        return blobClient switch
        {
            BlobClient genericClient => genericClient.OpenWrite(overwrite),
            BlockBlobClient blockClient => blockClient.OpenWrite(overwrite),
            _ => throw new InvalidOperationException()
        };
    }


}
