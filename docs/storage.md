<h1 align="center">Azure Storage</h1>

<p align="center">This library provides in-memory SDK for Azure Storage which can be used as a drop-in replacement for the official 
<a href="https://www.nuget.org/packages/Azure.Storage.Blobs" target="_blank">Azure.Storage.Blobs</a> and 
<a href="https://www.nuget.org/packages/Azure.Data.Tables" target="_blank">Azure.Data.Tables</a> SDKs in your tests. </p>

<p align="center">
    <a href="#example-usage">Example Usage</a> |
    <a href="#fault-injection">Fault Injection</a> |
    <a href="#supported-apis-and-features-for-blobs">Supported APIs and features for Blobs</a> |
    <a href="#supported-apis-and-features-for-tables">Supported APIs and features for Tables</a> |
    <a href="#available-fluent-assertions">Fluent Assertions</a>
</p>

> [!TIP]
> See the whole [In-Memory Azure Test SDK](../README.md) suite if you are interested in other Azure services.

## Example Usage

To get started, add `Spotflow.InMemory.Azure.Storage` package to your project.

```shell
dotnet add Spotflow.InMemory.Azure.Storage
```
This package provides in-memory implementation of Azure Storage SDK clients and models.
These in-memory implementations are inheriting the real Azure SDK types so you can use them as a drop-in replacement in your tests.
There is nothing special about the in-memory types, so they can be injected in many ways, e.g. via DI and constructor injection as demonstrated below.
Only extra step is to create parent `InMemoryStorageProvider` instance for the in-memory clients.

Let's consider the following type `ExampleService` as an example:

```cs
class ExampleService(BlobContainerClient containerClient)
{
    public async Task AddBlobToContainerAsync(BinaryData content, string blobName)
    {
        var blobClient = _containerClient.GetBlobClient(blobName);
        await blobClient.UploadAsync(content);
    }
}
```

The `ExampleService` might be constructed, for example, using DI:

```cs
// Setup DI - production configuration
var connectionString = "AccountName=testaccount;AccountKey=...";
var containerName = "test-container";

var services = new ServiceCollection();

services.AddSingleton<BlobContainerClient>(new BlobContainerClient(connectionString, containerName));
services.AddSingleton<ExampleService>();

...

// Use resulting service provider
var service = services.BuildServiceProvider().GetRequiredService<ExampleService>();
```

*Note:
Most frequently, the `new ServiceCollection()` and `.BuildServiceProvider()` will called by ASP.NET or other frameworks.
This is just an example of one of many ways how the in-memory clients can be used.*


To inject the in-memory implementation of `BlobContainerClient` to the `ExampleService` during test,
the `InMemoryBlobContainerClient` can be simply substituted for the real `BlobContainerClient` in the DI container:

```cs
// Setup DI - test-only configuration (additive)
var inMemoryProvider = new InMemoryStorageProvider();
var containerClient = new InMemoryBlobContainerClient(connectionString, containerName, inMemoryProvider);

services.AddSingleton<BlobContainerClient>(containerClient);
```

By default, the `InMemoryStorageProvider` is empty but exposes methods that allow to set up expected management-plane state:

```cs
inMemoryProvider.AddAccount("testaccount1");
inMemoryProvider.AddAccount("testaccount2");
```

To set up expected data-plane state, the `InMemoryBlobContainerClient` or other in-memory clients can be directly used:

```cs
containerClient.CreateIfNotExists();
```

## Fault Injection

Fault injections let you simulate transient and persistent faults in Azure Storage.
Thanks to that you can test how your application behaves in case of Azure outages, network issues, timeouts, etc.

To inject a fault, you need to use the [concept of hooks](hooks.md) - functions that are called before or after the actual operation is executed.
A new hook can be registered by calling the `AddHook` method on the `InMemoryStorageProvider` instance.
You can build fault hook by calling the `Faults` method on the hook context and then calling the appropriate method, e.g. `ServiceIsBusy`:

For overview of available hooks, please see the [Hooks](#hooks) section.

```cs
var provider = new InMemoryStorageProvider();
var hook = provider.AddHook(hookBuilder => hookBuilder.Before(ctx => ctx.Faults().ServiceIsBusy()))
```

The `AddHook` method gives you a builder that lets you define which operations the hook should apply to.
In the example above, the hook affects all storage operations.
However, you can limit it to specific operations, like `Download`, or target specific assets, such as blobs in a container named `my-container`:

```cs
var hook = provider.AddHook(
    hookBuilder => hookBuilder.ForBlobService()
        .ForBlobOperations(containerName: "my-container")
        .BeforeDownload(ctx => ctx.Faults().ServiceIsBusy())
    );
```

You can control when the hook should execute via the `IHookRegistration` interface returned by the `AddHook` method.
By default, the hook is enabled, but you can disable it by calling the `Disable` method.
To simulate temporary outages, use the `DisableAfter` method to limit the number of fault occurrences.

See a full example of fault injection below:

```cs
var provider = new InMemoryStorageProvider();

var hook = provider.AddHook(hook => hook.Before(ctx => ctx.Faults().ServiceIsBusy()));

var account = provider.AddAccount("test-account");

var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test-container");
var tableClient = InMemoryTableClient.FromAccount(account, "test-table");

var actBlob = () => containerClient.Create();
var actTable = () => tableClient.Create();

actBlob.Should().Throw<RequestFailedException>().WithMessage("Blob service in account 'test-account' is busy.");
actTable.Should().Throw<RequestFailedException>().WithMessage("Table service in account 'test-account' is busy.");

hook.Disable();

actBlob.Should().NotThrow();
actTable.Should().NotThrow();
```

## Delay Simulation

You can test how your application handles slow Azure responses by simulating delays.

Similar to fault injections, you can use [hooks](./hooks.md) to simulate delays.
To add a delay, call the `DelayAsync` method on the hook context.
The simplest way is to call the `DelayAsync` method with `TimeSpan` parameter, which specifies the duration of the delay.
Alternatively, you can use the `DelayAsync` method with a `IDelayGenerator` parameter, which allows you to specify the duration of the delay dynamically.

For overview of available hooks, please see the [Hooks](#hooks) section.

```cs
var provider = new InMemoryStorageProvider();

// Use static delay
provider.AddHook(hook => hook.Before(ctx => ctx.DelayAsync(TimeSpan.FromMilliseconds(100))));

// Use the built-in exponential delay generator
var delayGenerator = new ExponentialDelayGenerator();
provider.AddHook(hook => hook.Before(ctx => ctx.DelayAsync(delayGenerator)));
```

The simulated delays consider the time provider used when creating the `InMemoryStorageProvider`.
That way, you have full control over the time in your tests.
See a full example of delay simulation below:

```cs
var timeProvider = new FakeTimeProvider();

var provider = new InMemoryStorageProvider(timeProvider: timeProvider);

provider.AddHook(hook => hook.Before(ctx => ctx.DelayAsync(TimeSpan.FromMilliseconds(100))));

var account = provider.AddAccount("test-account");

var containerClient = InMemoryBlobContainerClient.FromAccount(account, "test-container");

var task = Task.Run(() => containerClient.Create());

while (task.Status != TaskStatus.Running)
{
    await Task.Delay(10);
}

await Task.Delay(1000);

task.Status.Should().Be(TaskStatus.Running);

timeProvider.Advance(TimeSpan.FromSeconds(32));

var response = await task;

response.Value.LastModified.Should().Be(timeProvider.GetUtcNow());
```

## Supported APIs and features for Blobs

### SDK clients & methods

Following SDK clients and their method groups and properties are supported.

Async versions of these methods are also supported. All supported async methods starts with [Task.Yield()](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.yield) to force the method to complete asynchronously.

Other methods and properties are not supported and will throw `NotSupportedException`.

Clients are thread-safe.

#### `InMemoryBlobServiceClient: BlobServiceClient`

| Property                   | Note                    |
| -------------------------- | ----------------------- |
| `AccountName`              |                         |
| `CanGenerateAccountSasUri` | Always returns `false`. |
| `Name`                     |                         |
| `Uri`                      |                         |

| Method group             |
| ------------------------ |
| `GetBlobContainerClient` |

| Constructors & factory methods                | Note                          |
| --------------------------------------------- | ----------------------------- |
| `(string connectionString)`                   | No credentials are validated  |
| `(Uri serviceUri)`                            | No credentials are validated. |
| `FromAccount(InMemoryStorageAccount account)` |                               |

#### `InMemoryBlobContainerClient: BlobContainerClient`

| Property            | Note                    |
| ------------------- | ----------------------- |
| `AccountName`       |                         |
| `CanGenerateSasUri` | Always returns `false`. |
| `Name`              |                         |
| `Uri`               |                         |

| Method group                 | Note                                                                                           |
| ---------------------------- | ---------------------------------------------------------------------------------------------- |
| `Create`                     |                                                                                                |
| `CreateIfNotExists`          |                                                                                                |
| `DeleteBlob`                 |                                                                                                |
| `DeleteBlobIfExists`         |                                                                                                |
| `Exists`                     |                                                                                                |
| `GetBlobClient`              |                                                                                                |
| `GetBlockBlobClient`         |                                                                                                |
| `GetBlobs`                   | The `BlobTraits` and `BlobStates` parameters are ignored except `BlobStates.Uncommitted` flag. |
| `GetBlobsByHierarchy`        | The `BlobTraits` and `BlobStates` parameters are ignored except `BlobStates.Uncommitted` flag. |
| `GetParentBlobServiceClient` |                                                                                                |
| `GetProperties`              |                                                                                                |
| `UploadBlob`                 |                                                                                                |

| Constructors & factory methods                                          | Note                          |
| ----------------------------------------------------------------------- | ----------------------------- |
| `(string connectionString, string blobContainerName)`                   | No credentials are validated  |
| `(Uri blobContainerUri)`                                                | No credentials are validated. |
| `FromAccount(InMemoryStorageAccount account, string blobContainerName)` |                               |

#### `InMemoryBlobClient: BlobClient`

| Property            | Note                    |
| ------------------- | ----------------------- |
| `AccountName`       |                         |
| `BlobContainerName` |                         |
| `CanGenerateSasUri` | Always returns `true`. |
| `Name`              |                         |
| `Uri`               |                         |

| Method group                   | Note                                                        |
| ------------------------------ | ----------------------------------------------------------- |
| `Delete`                       | Only supported for `DeleteSnapshotsOption.None`             |
| `DeleteIfExistsAsync`          | Only supported for `DeleteSnapshotsOption.None`             |
| `Download`                     | Overloads with `HttpRange` parameter are not supported.     |
| `DownloadStreaming`            | Overloads with `HttpRange` parameter are not supported.     |
| `DownloadContent`              | Overloads with `HttpRange` parameter are not supported.     |
| `Exists`                       |                                                             |
| `GetParentBlobContainerClient` |                                                             |
| `GetProperties`                |                                                             |
| `OpenRead`                     | Seeking on the returned stream is not supported.            |
| `OpenWrite`                    |                                                             |
| `Upload`                       | Overloads accepting path to a local file are not supported. |

| Constructors & factory methods                                                           | Note                          |
| ---------------------------------------------------------------------------------------- | ----------------------------- |
| `(string connectionString, string blobContainerName, string blobName)`                   | No credentials are validated  |
| `(Uri blobUri)`                                                                          | No credentials are validated. |
| `FromAccount(InMemoryStorageAccount account, string blobContainerName, string blobName)` |                               |

#### `InMemoryBlockBlobClient: BlockBlobClient`

| Property                          | Note                    |
| --------------------------------- | ----------------------- |
| `AccountName`                     |                         |
| `BlobContainerName`               |                         |
| `BlockBlobMaxUploadBlobBytes`     |                         |
| `BlockBlobMaxUploadBlobLongBytes` |                         |
| `BlockBlobMaxStageBlockBytes`     |                         |
| `BlockBlobMaxStageBlockLongBytes` |                         |
| `BlockBlobMaxBlocks`              |                         |
| `CanGenerateSasUri`               | Always returns `true`. |
| `Name`                            |                         |
| `Uri`                             |                         |

| Method group                   | Note                                                    |
| ------------------------------ | ------------------------------------------------------- |
| `CommitBlockList`              |                                                         |
| `Delete`                       | Only supported for `DeleteSnapshotsOption.None`         |
| `DeleteIfExistsAsync`          | Only supported for `DeleteSnapshotsOption.None`         |
| `Download`                     | Overloads with `HttpRange` parameter are not supported. |
| `DownloadContent`              | Overloads with `HttpRange` parameter are not supported. |
| `DownloadStreaming`            | Overloads with `HttpRange` parameter are not supported. |
| `Exists`                       |                                                         |
| `GetBlockList`                 |                                                         |
| `GetParentBlobContainerClient` |                                                         |
| `GetProperties`                |                                                         |
| `OpenRead`                     | Seeking on the returned stream is not supported.        |
| `OpenWrite`                    |                                                         |
| `StageBlock`                   |                                                         |
| `Upload`                       |                                                         |

| Constructors & factory methods                                                           | Note                          |
| ---------------------------------------------------------------------------------------- | ----------------------------- |
| `(string connectionString, string blobContainerName, string blobName)`                   | No credentials are validated  |
| `(Uri blobUri)`                                                                          | No credentials are validated. |
| `FromAccount(InMemoryStorageAccount account, string blobContainerName, string blobName)` |                               |

### Features

For the supported methods enumerated above, not all features are fully implemented.

If the given feature is not supported, than the method will just ignore any parameters related to that feature.

| Feature                          | Is Supported |
| -------------------------------- | ------------ |
| Access tiers                     | ❌           |
| Client-side encryption           | ❌           |
| Condition - `IfMatch`            | ✅           |
| Condition - `IfModifiedSince`    | ❌           |
| Condition - `IfNoneMatch`        | ✅           |
| Condition - `IfUnmodifiedSince`  | ❌           |
| Connection string key validation | ❌           |
| CORS                             | ❌           |
| Encryption scopes                | ❌           |
| Header - `Content-Encoding`      | ✅           |
| Header - `Content-Type`          | ✅           |
| Header - Others                  | ❌           |
| Immutability policies            | ❌           |
| Leases                           | ❌           |
| Legal holds                      | ❌           |
| Metadata (blob)                  | ✅           |
| Metadata (container)             | ✅           |
| Progress handling                | ❌           |
| Public access                    | ❌           |
| Query                            | ❌           |
| Ranges                           | ❌           |
| SAS URI signature validation     | ❌           |
| Server-side copy                 | ❌           |
| Snapshots                        | ❌           |
| Soft delete                      | ❌           |
| Static website                   | ❌           |
| Tags                             | ❌           |
| Transfer (validation) options    | ❌           |
| Transfer options                 | ❌           |
| Versions                         | ❌           |

## Supported APIs and features for Tables

### SDK clients & methods

Following SDK clients and their method groups and properties are supported.

Async versions of these methods are also supported. All supported async methods starts with [Task.Yield()](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.yield) to force the method to complete asynchronously.

Other methods and properties are not supported and will throw `NotSupportedException`.

Clients are thread-safe.

#### `InMemoryTableServiceClient : TableServiceClient`

| Property      | Note |
| ------------- | ---- |
| `AccountName` |      |
| `Uri`         |      |

| Method group     | Note |
| ---------------- | ---- |
| `GetTableClient` |      |

| Constructor                 | Note                          |
| --------------------------- | ----------------------------- |
| `(string connectionString)` | No credentials are validated  |
| `(Uri tableServiceUri)`     | No credentials are validated. |

#### `InMemoryTableClient : TableClient`

| Property      | Note |
| ------------- | ---- |
| `AccountName` |      |
| `Name`        |      |
| `Uri`         |      |

| Method group        | Note |
| ------------------- | ---- |
| `Create`            |      |
| `CreateIfNotExists` |      |
| `GetSasBuilder`     |      |
| `GenerateSasUri`    |      |
| `Query`             |      |
| `AddEntity`         |      |
| `UpsertEntity`      |      |
| `UpdateEntity`      |      |
| `DeleteEntity`      |      |
| `SubmitTransaction` |      |

| Constructor                                   | Note                          |
| --------------------------------------------- | ----------------------------- |
| `(string connectionString, string tableName)` | No credentials are validated  |
| `(Uri tableServiceUri, string tableName)`     | No credentials are validated. |
| `(Uri tableUri)`                              | No credentials are validated. |

### Features

For the supported methods enumerated above, not all features are fully implemented.

If the given feature is not supported, than the method will just ignore any parameters related to that feature.

| Feature                          | Is Supported |
| -------------------------------- | ------------ |
| Access policy                    | ❌           |
| Condition - `IfMatch`            | ✅           |
| Connection string key validation | ❌           |
| Query - String                   | ✅           |
| Query - LINQ                     | ✅           |
| Query - Property selectors       | ❌           |
| SAS URI signature validation     | ❌           |
| Transactions                     | ✅           |
| Update mode - Merge              | ✅           |
| Update mode - Replace            | ✅           |

## Available Fluent Assertions

Namespace: `Spotflow.InMemory.Azure.Storage.FluentAssertions`

### `BlobClientBase`

`.Should()`

-   `.Exist()`: asserts that the blob exist at the given time..
-   `.Exist(TimeSpan waitTime)`: returns immediately if the blob exist or waits for some time for the blob to be deleted before failing.
-   `.MatchName(string expectedNamePattern)`: asserts that the blob name matches expected FluentAssertion wildcard pattern.
-   `.HaveSize()`: asserts that blob size equals to expected size.
-   `.BeEmpty()`: asserts that blob size is 0.
-   `.HaveContent(string expectedContent)`: interprets blob content as UTF-8 string and asserts it equals to the expected content.
-   `.HaveCommittedBlocks(int expectedCount)`: asserts that blob has expected number of committed blocks.
-   `.HaveCommittedBlocksWithSizes(int?[] expectedBlockSizes)`: asserts that blob has expected number of committed blocks with expected sizes.
-   `.HaveCommittedBlock(int blockOrdinal, Action<BlobBlock> blockAssertion)`: asserts that blob has committed block with expected properties.
-   `.HaveNoCommittedBlocks()`: asserts that blob has no committed blocks.
-   `.HaveUncommittedBlocks(int expectedCount)`: asserts that blob has expected number of uncommitted blocks.
-   `.HaveNoUncommittedBlocks()`: asserts that blob has no uncommitted blocks.

## Hooks

Following hooks are supported in both `Before` and `After` variants:

-   All `Storage` operations
    -   All `Blob Service` operations
        -   All `Blob` operations
            -   `Download`
            -   `Upload`
            -   `OpenRead`
            -   `OpenWrite`
        -   All `Container` operations
            -   `Create` / `CreateIfNotExists`
    -   All `Table Service` operations
        -   All `Entity` operations
            -   `Add`
            -   `Upsert`
        -   All `Table` operations
            -   `Create`
            -   `Query`

For details about concept of hooks, please see the [Hooks](./hooks.md) page.
