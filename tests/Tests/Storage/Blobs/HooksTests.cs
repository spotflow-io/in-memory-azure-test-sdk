using Azure;
using Azure.Storage.Blobs.Models;

using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;
using Spotflow.InMemory.Azure.Storage.Blobs.Hooks;
using Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;

namespace Tests.Storage.Blobs;

[TestClass]
public class HooksTests
{
    [TestMethod]
    public async Task Blob_Download_Hooks_Should_Execute()
    {
        const string accountName = "test-account";
        const string containerName = "test-container";
        const string blobName = "test-blob";
        var provider = new InMemoryStorageProvider();

        BlobDownloadBeforeHookContext? capturedBeforeContext = null;
        BlobDownloadAfterHookContext? capturedAfterContext = null;

        provider.AddHook(builder => builder.ForBlobService().ForBlobOperations().BeforeDownload(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }));

        provider.AddHook(builder => builder.ForBlobService().ForBlobOperations().AfterDownload(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }));

        var account = provider.AddAccount(accountName);
        var containerClient = InMemoryBlobContainerClient.FromAccount(account, containerName);
        await containerClient.CreateIfNotExistsAsync();
        var client = InMemoryBlobClient.FromAccount(account, containerName, blobName);

        var blobData = new BinaryData("test");
        await client.UploadAsync(blobData);

        await client.DownloadContentAsync(new BlobRequestConditions() { IfMatch = ETag.All }, default);


        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext?.StorageAccountName.Should().BeEquivalentTo(accountName);
        capturedBeforeContext?.ContainerName.Should().BeEquivalentTo(containerName);
        capturedBeforeContext?.BlobName.Should().BeEquivalentTo(blobName);
        capturedBeforeContext?.Operation.Should().Be(BlobOperations.Download);
        capturedBeforeContext?.Options?.Conditions.IfMatch.Should().Be(ETag.All);

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.StorageAccountName.Should().BeEquivalentTo(accountName);
        capturedAfterContext?.ContainerName.Should().BeEquivalentTo(containerName);
        capturedAfterContext?.BlobName.Should().BeEquivalentTo(blobName);
        capturedAfterContext?.Operation.Should().Be(BlobOperations.Download);
        capturedAfterContext?.BlobDownloadDetails.Should().NotBeNull();
        capturedAfterContext?.Content.ToString().Should().Be(blobData.ToString());
    }

    [TestMethod]
    public async Task Hooks_With_Different_Scope_Should_Not_Execute()
    {
        const string accountName = "test-account";
        const string containerName = "test-container";
        const string blobName = "test-blob";
        var provider = new InMemoryStorageProvider();

        HookFunc<BlobServiceBeforeHookContext> failingBeforeHook = _ => throw new InvalidOperationException("This hook should not execute.");
        HookFunc<BlobServiceAfterHookContext> failingAfterHook = _ => throw new InvalidOperationException("This hook should not execute.");

        provider.AddHook(hook => hook.ForBlobService("different-acc").ForBlobOperations().BeforeDownload(failingBeforeHook));
        provider.AddHook(hook => hook.ForBlobService("different-acc").ForBlobOperations().BeforeDownload(failingBeforeHook));
        provider.AddHook(hook => hook.ForBlobService().ForBlobOperations(blobName: "different-blob").BeforeDownload(failingBeforeHook));
        provider.AddHook(hook => hook.ForBlobService().ForBlobOperations(containerName: "different-container").BeforeDownload(failingBeforeHook));

        provider.AddHook(hook => hook.ForBlobService("different-acc").ForBlobOperations().AfterDownload(failingAfterHook));
        provider.AddHook(hook => hook.ForBlobService("different-acc").ForBlobOperations().AfterDownload(failingAfterHook));
        provider.AddHook(hook => hook.ForBlobService().ForBlobOperations(blobName: "different-blob").AfterDownload(failingAfterHook));
        provider.AddHook(hook => hook.ForBlobService().ForBlobOperations(containerName: "different-container").AfterDownload(failingAfterHook));

        var account = provider.AddAccount(accountName);
        var containerClient = InMemoryBlobContainerClient.FromAccount(account, containerName);
        await containerClient.CreateIfNotExistsAsync();
        var client = InMemoryBlobClient.FromAccount(account, containerName, blobName);

        var blobData = new BinaryData("test");

        await client.UploadAsync(blobData);
        await client.DownloadContentAsync(default);
    }

    [TestMethod]
    public async Task Parent_Hook_Should_Execute()
    {
        const string accountName = "test-account";
        const string containerName = "test-container";
        const string blobName = "test-blob";
        var provider = new InMemoryStorageProvider();

        BlobServiceBeforeHookContext? capturedBeforeContext = null;
        BlobServiceAfterHookContext? capturedAfterContext = null;

        provider.AddHook(builder => builder.ForBlobService().Before(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }));

        provider.AddHook(builder => builder.ForBlobService().After(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }));

        var account = provider.AddAccount(accountName);
        var containerClient = InMemoryBlobContainerClient.FromAccount(account, containerName);
        await containerClient.CreateIfNotExistsAsync();
        var client = InMemoryBlobClient.FromAccount(account, containerName, blobName);

        var blobData = new BinaryData("test");

        await client.UploadAsync(blobData);
        await client.DownloadContentAsync(default);


        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext?.StorageAccountName.Should().Be(accountName);

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.StorageAccountName.Should().Be(accountName);

    }

    [TestMethod]
    public async Task Targeted_Hooks_Should_Execute()
    {
        const string accountName = "test-account";
        const string containerName = "test-container";
        const string blobName = "test-blob";
        var provider = new InMemoryStorageProvider();

        BlobBeforeHookContext? capturedBeforeContext = null;
        BlobAfterHookContext? capturedAfterContext = null;

        provider.AddHook(builder => builder.ForBlobService().ForBlobOperations().Before(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }, BlobOperations.Upload));

        provider.AddHook(builder => builder.ForBlobService().ForBlobOperations().After(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }, BlobOperations.Upload));

        var account = provider.AddAccount(accountName);
        var containerClient = InMemoryBlobContainerClient.FromAccount(account, containerName);
        await containerClient.CreateIfNotExistsAsync();
        var client = InMemoryBlobClient.FromAccount(account, containerName, blobName);

        var blobData = new BinaryData("test");

        await client.UploadAsync(blobData);

        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext?.StorageAccountName.Should().Be(accountName);
        capturedBeforeContext?.ContainerName.Should().Be(containerName);
        capturedBeforeContext?.BlobName.Should().Be(blobName);

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.StorageAccountName.Should().Be(accountName);
        capturedAfterContext?.ContainerName.Should().Be(containerName);
        capturedAfterContext?.BlobName.Should().Be(blobName);

    }

    [TestMethod]
    public async Task Hooks_With_Different_Target_Should_Not_Execute()
    {
        const string accountName = "test-account";
        const string containerName = "test-container";
        const string blobName = "test-blob";
        var provider = new InMemoryStorageProvider();

        HookFunc<BlobServiceBeforeHookContext> failingBeforeHook = _ => throw new InvalidOperationException("This hook should not execute.");
        HookFunc<BlobServiceAfterHookContext> failingAfterHook = _ => throw new InvalidOperationException("This hook should not execute.");

        provider.AddHook(hook => hook.ForBlobService().Before(failingBeforeHook, containerOperations: ContainerOperations.None, blobOperations: BlobOperations.Download));
        provider.AddHook(hook => hook.ForBlobService().After(failingAfterHook, containerOperations: ContainerOperations.None, blobOperations: BlobOperations.Download));

        var account = provider.AddAccount(accountName);
        var containerClient = InMemoryBlobContainerClient.FromAccount(account, containerName);
        await containerClient.CreateIfNotExistsAsync();

        var client = InMemoryBlobClient.FromAccount(account, containerName, blobName);

        var blobData = new BinaryData("test");

        await client.UploadAsync(blobData);
    }
}
