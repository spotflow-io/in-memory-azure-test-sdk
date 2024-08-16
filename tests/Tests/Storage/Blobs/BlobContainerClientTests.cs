using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;
using Spotflow.InMemory.Azure.Storage.Resources;

using Tests.Utils;

namespace Tests.Storage.Blobs;

[TestClass]
public class BlobContainerClientTests
{
    [TestMethod]
    public void Constructor_With_Connection_String_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var connectionString = account.CreateConnectionString();

        var client = new InMemoryBlobContainerClient(connectionString, "test", provider);

        AssertClientProperties(client, "test", account);
    }

    [TestMethod]
    public void Constructor_With_Uri_Should_Succeed()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var client = new InMemoryBlobContainerClient(account.CreateBlobContainerSasUri("test"), provider);

        AssertClientProperties(client, "test", account);
    }

    [TestMethod]
    public void Constructor_With_Uri_Without_Container_Should_Fail()
    {
        var provider = new InMemoryStorageProvider();

        var account = provider.AddAccount();

        var act = () => new InMemoryBlobContainerClient(account.BlobServiceUri, provider);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("Blob container name must be specified when creating a blob container client.");
    }

    [TestMethod]
    public void Construct_From_Account_Should_Succeed()
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var client = InMemoryBlobContainerClient.FromAccount(account, "test");

        AssertClientProperties(client, "test", account);
    }

    private static void AssertClientProperties(InMemoryBlobContainerClient client, string expectedContainerName, InMemoryStorageAccount account)
    {
        var expectedUri = new Uri(account.BlobServiceUri, expectedContainerName);

        client.Uri.Should().Be(expectedUri);
        client.AccountName.Should().Be(account.Name);
        client.Name.Should().Be(expectedContainerName);
        client.CanGenerateSasUri.Should().BeFalse();
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Create_From_Service_Client_With_Invalid_Name_Should_Fail()
    {
        var serviceClient = ImplementationProvider.GetBlobServiceClient();

        var containerName = "abc--def";

        var containerClient = serviceClient.GetBlobContainerClient(containerName);

        var act = () => containerClient.Create();

        act.Should()
            .Throw<RequestFailedException>()
            .Where(e => e.ErrorCode == "InvalidResourceName");

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task GetBlobClient_Should_Return_Working_Client()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(BinaryData.FromString("test-data"));

        blobClient.DownloadContent().Value.Content.ToString().Should().Be("test-data");

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Exists_For_Non_Existing_Container_Should_Be_False()
    {
        var serviceClient = ImplementationProvider.GetBlobServiceClient();

        var containerName = Guid.NewGuid().ToString();

        var containerClient = serviceClient.GetBlobContainerClient(containerName);

        containerClient.Exists().Value.Should().BeFalse();
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Exists_For_Existing_Container_Should_Be_True()
    {
        var serviceClient = ImplementationProvider.GetBlobServiceClient();

        var containerName = nameof(Exists_For_Existing_Container_Should_Be_True)
            .ToLowerInvariant()
            .Replace("_", "-");

        var containerClient = serviceClient.GetBlobContainerClient(containerName);

        containerClient.CreateIfNotExists();

        containerClient.Exists().Value.Should().BeTrue();
    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(10, 1, BlobStates.None, 10)]
    [DataRow(10, 1, BlobStates.Uncommitted, 11)]
    public void GetBlobs_Should_Return_Existing_Relevant_Blobs(int commitedCount, int uncommitedCount, BlobStates states, int expectedTotalCount)
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobNamePrefix = Guid.NewGuid().ToString();

        for (var i = 0; i < commitedCount; i++)
        {
            var blobClient = containerClient.GetBlobClient($"{blobNamePrefix}_test-blob-commited-{i:D10}");
            blobClient.Upload(BinaryData.FromString("test"));
        }

        for (var i = 0; i < uncommitedCount; i++)
        {
            var blockBlobClient = containerClient.GetBlockBlobClient($"{blobNamePrefix}_test-blob-uncommited-{i:D10}");
            blockBlobClient.StageBlock(Convert.ToBase64String([1]), BinaryData.FromString("test").ToStream());
        }

        containerClient.GetBlobs(prefix: blobNamePrefix, states: states)
            .Should()
            .HaveCount(expectedTotalCount);
    }

    [TestMethod]
    [DataRow(10_000, 2000, true)]
    [DataRow(10_000, 2000, false)]
    [DataRow(10_000, 128, true)]
    [DataRow(10_000, 128, false)]
    [DataRow(49_851, 128, true)]
    [DataRow(49_851, 128, false)]
    [DataRow(123, 2000, true)]
    [DataRow(123, 2000, false)]
    public async Task GetBlobs_Should_Return_Existing_Blobs_In_Pages(int numberOfBlobs, int pageSizeHint, bool async)
    {
        var account = new InMemoryStorageProvider().AddAccount();

        var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test");
        containerClient.Create();

        for (var i = 0; i < numberOfBlobs; i++)
        {
            var blobClient = containerClient.GetBlobClient($"blob-{i:D10}");
            blobClient.Upload(BinaryData.FromString("test"));
        }

        containerClient.GetBlobs().Should().HaveCount(numberOfBlobs);

        var expectedNumberOfPages = (int) Math.Ceiling((double) numberOfBlobs / pageSizeHint);

        var actualPageCount = await getPageCountAsync(containerClient, pageSizeHint, async);

        actualPageCount.Should().Be(expectedNumberOfPages);

        var firstPage = await getPageAsync(containerClient, 0, null, pageSizeHint, async);

        if (pageSizeHint >= numberOfBlobs)
        {
            firstPage.ContinuationToken.Should().BeNull();
            firstPage.Values.Should().HaveCount(numberOfBlobs);
            return;
        }

        firstPage.ContinuationToken.Should().Be("page-1");
        firstPage.Values.Should().HaveCount(pageSizeHint);
        firstPage.Values[0].Name.Should().Be("blob-0000000000");
        firstPage.Values[pageSizeHint - 1].Name.Should().Be($"blob-{pageSizeHint - 1:D10}");

        var secondPage = await getPageAsync(containerClient, 0, firstPage.ContinuationToken, pageSizeHint, async);

        secondPage.ContinuationToken.Should().Be("page-2");
        secondPage.Values.Should().HaveCount(pageSizeHint);
        secondPage.Values[0].Name.Should().Be($"blob-{pageSizeHint:D10}");
        secondPage.Values[pageSizeHint - 1].Name.Should().Be($"blob-{(pageSizeHint * 2) - 1:D10}");

        var lastPage = await getPageAsync(containerClient, expectedNumberOfPages - 1 - 2, secondPage.ContinuationToken, pageSizeHint, async);

        lastPage.ContinuationToken.Should().BeNull();
        lastPage.Values.Should().HaveCount(numberOfBlobs % pageSizeHint == 0 ? pageSizeHint : numberOfBlobs % pageSizeHint);


        static async ValueTask<int> getPageCountAsync(BlobContainerClient client, int pageSizeHint, bool async)
        {
            if (async)
            {
                var count = 0;

                await foreach (var page in client.GetBlobsAsync().AsPages(null, pageSizeHint))
                {
                    count++;
                }

                return count;
            }
            else
            {
                return client.GetBlobs().AsPages(null, pageSizeHint).Count();
            }
        }

        static async ValueTask<Page<BlobItem>> getPageAsync(BlobContainerClient client, int pageIndex, string? continuationToken, int pageSizeHint, bool async)
        {
            if (async)
            {
                var currentPageIndex = 0;

                await foreach (var page in client.GetBlobsAsync().AsPages(continuationToken, pageSizeHint))
                {
                    if (currentPageIndex == pageIndex)
                    {
                        return page;
                    }

                    currentPageIndex++;
                }

                throw new InvalidOperationException($"No page at index {pageIndex} not found.");
            }
            else
            {
                return client.GetBlobs().AsPages(continuationToken, pageSizeHint).ElementAt(pageIndex);
            }
        }

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Upload_Blob_Should_Succeed()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        containerClient.UploadBlob(blobName, BinaryData.FromString("test"));

        containerClient.GetBlobClient(blobName).DownloadContent().Value.Content.ToString().Should().Be("test");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public void Delete_Blob_Should_Succeed()
    {
        var containerClient = ImplementationProvider.GetBlobContainerClient();

        containerClient.CreateIfNotExists();

        var blobName = Guid.NewGuid().ToString();

        containerClient.UploadBlob(blobName, BinaryData.FromString("test"));

        containerClient.GetBlobs(prefix: blobName).Should().HaveCount(1);

        containerClient.DeleteBlob(blobName);

        containerClient.GetBlobs(prefix: blobName).Should().BeEmpty();
    }
}
