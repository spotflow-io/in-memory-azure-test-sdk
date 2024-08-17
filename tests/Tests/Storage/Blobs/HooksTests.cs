using System.Buffers;
using System.Security.Cryptography;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;
using Spotflow.InMemory.Azure.Storage.Blobs.Hooks;
using Spotflow.InMemory.Azure.Storage.Blobs.Hooks.Contexts;
using Spotflow.InMemory.Azure.Storage.Hooks.Contexts;

using Tests.Utils;

using static Tests.Storage.Blobs.BlobClientTests;

namespace Tests.Storage.Blobs;

[TestClass]
public class HooksTests
{
    [TestMethod]
    [DataRow(BlobClientType.Block)]
    [DataRow(BlobClientType.Generic)]
    public async Task Blob_Download_Hooks_Should_Execute(BlobClientType clientType)
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

        var data = new BinaryData("test");

        containerClient.UploadBlob(blobName, data);

        var client = containerClient.GetBlobBaseClient(blobName, clientType);

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
        capturedAfterContext?.BlobProperties.Should().NotBeNull();
        capturedAfterContext?.Content.ToString().Should().Be(data.ToString());
    }


    [TestMethod]
    [DataRow(BlobClientType.Block)]
    [DataRow(BlobClientType.Generic)]
    public void Blob_OpenRead_Hooks_Should_Execute(BlobClientType clientType)
    {
        StorageBeforeHookContext? capturedBeforeContextGeneric = null;
        BlobOpenReadBeforeHookContext? capturedBeforeContextSpecific = null;
        StorageAfterHookContext? capturedAfterContextGeneric = null;
        BlobOpenReadAfterHookContext? capturedAfterContextSpecific = null;

        var provider = new InMemoryStorageProvider();

        provider.AddHook(hook => hook.Before(ctx => { capturedBeforeContextGeneric = ctx; return Task.CompletedTask; }));
        provider.AddHook(hook => hook.ForBlobService().ForBlobOperations().BeforeOpenRead(ctx => { capturedBeforeContextSpecific = ctx; return Task.CompletedTask; }));
        provider.AddHook(hook => hook.ForBlobService().ForBlobOperations().AfterOpenRead(ctx => { capturedAfterContextSpecific = ctx; return Task.CompletedTask; }));
        provider.AddHook(hook => hook.After(ctx => { capturedAfterContextGeneric = ctx; return Task.CompletedTask; }));

        var account = provider.AddAccount();

        var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test-container");

        containerClient.Create();

        var data = new BinaryData("test");

        var blobName = "test-blob";

        containerClient.UploadBlob(blobName, data);

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        using var stream = blobClient.OpenRead();

        capturedBeforeContextSpecific.Should().NotBeNull();
        capturedBeforeContextGeneric.Should().BeOfType<BlobOpenReadBeforeHookContext>();

        capturedAfterContextSpecific.Should().NotBeNull();
        capturedAfterContextGeneric.Should()
            .BeOfType<BlobOpenReadAfterHookContext>()
            .Which.Content.ToString().Should().Be(data.ToString());
    }

    [TestMethod]
    [DataRow(BlobClientType.Block)]
    [DataRow(BlobClientType.Generic)]
    public void Blob_OpenWrite_Hooks_Should_Execute(BlobClientType clientType)
    {
        StorageBeforeHookContext? capturedBeforeContextGeneric = null;
        BlobOpenWriteBeforeHookContext? capturedBeforeContextSpecific = null;
        StorageAfterHookContext? capturedAfterContextGeneric = null;
        BlobOpenWriteAfterHookContext? capturedAfterContextSpecific = null;

        var provider = new InMemoryStorageProvider();

        provider.AddHook(hook => hook.Before(ctx => { capturedBeforeContextGeneric = ctx; return Task.CompletedTask; }));
        provider.AddHook(hook => hook.ForBlobService().ForBlobOperations().BeforeOpenWrite(ctx => { capturedBeforeContextSpecific = ctx; return Task.CompletedTask; }));
        provider.AddHook(hook => hook.ForBlobService().ForBlobOperations().AfterOpenWrite(ctx => { capturedAfterContextSpecific = ctx; return Task.CompletedTask; }));
        provider.AddHook(hook => hook.After(ctx => { capturedAfterContextGeneric = ctx; return Task.CompletedTask; }));

        var account = provider.AddAccount();

        var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test-container");

        containerClient.Create();

        var blobName = "test-blob";

        var blobClient = containerClient.GetBlobBaseClient(blobName, clientType);

        using var stream = blobClient switch
        {
            BlockBlobClient blockClient => blockClient.OpenWrite(true),
            BlobClient genericClient => genericClient.OpenWrite(true),
            _ => throw new InvalidOperationException()
        };

        capturedBeforeContextSpecific.Should().NotBeNull();
        capturedBeforeContextGeneric.Should().BeOfType<BlobOpenWriteBeforeHookContext>();

        capturedAfterContextSpecific.Should().NotBeNull();
        capturedAfterContextGeneric.Should().BeOfType<BlobOpenWriteAfterHookContext>();
    }


    [TestMethod]
    public void Blob_OpenWrite_Hooks_With_Interceptor_Should_Execute()
    {
        var provider = new InMemoryStorageProvider();

        var interceptor = new TestBlobWriteStreamInterceptor();

        provider.AddHook(hook => hook.ForBlobService().ForBlobOperations().AfterOpenWrite(ctx =>
        {
            ctx.AddStreamInterceptor(interceptor);
            return Task.CompletedTask;
        }));

        var account = provider.AddAccount();

        var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test-container");

        containerClient.Create();

        var blobClient = containerClient.GetBlockBlobClient("test-blob");

        using var stream = blobClient.OpenWrite(true);

        var data = new byte[1024 * 1024 * 8];

        RandomNumberGenerator.Fill(data);

        stream.Write(data);

        stream.Flush();

        stream.Dispose();

        interceptor.WriteCount.Should().Be(1);
        interceptor.Buffer.Length.Should().Be(data.Length);
        interceptor.FlushCount.Should().Be(4);
        interceptor.DisposeCount.Should().Be(1);

    }

    private class TestBlobWriteStreamInterceptor : IBlobWriteStreamInterceptor
    {
        public int DisposeCount { get; private set; }
        public int FlushCount { get; private set; }
        public int WriteCount { get; private set; }
        private readonly ArrayBufferWriter<byte> _buffer = new();
        public ReadOnlyMemory<byte> Buffer => _buffer.WrittenMemory;

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }

        public Task FlushAsync()
        {
            FlushCount++;
            return Task.CompletedTask;
        }

        public Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            WriteCount++;
            _buffer.Write(buffer.Span);
            return Task.CompletedTask;
        }
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
