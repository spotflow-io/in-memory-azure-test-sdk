![](HeroImage.jpg)

<h1 align="center">Azure In-Memory SDKs for Testing</h1>
<p align="center">Drop-in fakes of Azure .NET SDKs to make your test blazing-fast and reliable.</p>

<p align="center">
    <a href="#supported-sdks">Supported SDKs</a> |
    <a href="#example">Example</a> |
    <a href="#key-features">Key features</a> |
    <a href="#why-should-i-use-it">Why Should I Use It?</a> |
    <a href="#how-it-works">How It Works</a> |
    <a href="#license">License</a>
</p>

![CI status](https://github.com/spotflow-io/in-memory-azure-test-sdk/actions/workflows/ci.yml/badge.svg?branch=main)

## Supported SDKs

| Package                                                                          | Relevant Azure SDK                                                                                                                                                                                           | NuGet                                                                                                                                                                                  |
| -------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [**Spotflow.InMemory.Azure.Storage**](./docs/storage.md)                         | [Azure.Storage.Blobs](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/storage.blobs-readme), [Azure.Data.Tables](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/data.tables-readme) | [![NuGet](https://img.shields.io/nuget/v/Spotflow.InMemory.Azure.Storage.svg)](https://www.nuget.org/packages/Spotflow.InMemory.Azure.Storage)                                         |
| [**Spotflow.InMemory.Azure.Storage.FluentAssertions**](./docs/storage.md)        |                                                                                                                                                                                                              | [![NuGet](https://img.shields.io/nuget/v/Spotflow.InMemory.Azure.Storage.FluentAssertions.svg)](https://www.nuget.org/packages/Spotflow.InMemory.Azure.Storage.FluentAssertions)       |
| [**Spotflow.InMemory.Azure.EventHubs**](./docs/event-hubs.md)                    | [Azure.Messaging.EventHubs](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/event-hubs)                                                                                                          | [![NuGet](https://img.shields.io/nuget/v/Spotflow.InMemory.Azure.EventHubs.svg)](https://www.nuget.org/packages/Spotflow.InMemory.Azure.EventHubs)                                     |
| [**Spotflow.InMemory.Azure.ServiceBus**](./docs/service-bus.md)                  | [Azure.Messaging.ServiceBus](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/microsoft.servicebus-readme)                                                                                        | [![NuGet](https://img.shields.io/nuget/v/Spotflow.InMemory.Azure.ServiceBus.svg)](https://www.nuget.org/packages/Spotflow.InMemory.Azure.ServiceBus)                                   |
| [**Spotflow.InMemory.Azure.ServiceBus.FluentAssertions**](./docs/service-bus.md) |                                                                                                                                                                                                              | [![NuGet](https://img.shields.io/nuget/v/Spotflow.InMemory.Azure.ServiceBus.FluentAssertions.svg)](https://www.nuget.org/packages/Spotflow.InMemory.Azure.ServiceBus.FluentAssertions) |
| [**Spotflow.InMemory.Azure.KeyVault**](./docs/key-vault.md)                      | [Azure.Security.KeyVault.Secrets](https://learn.microsoft.com/en-us/dotnet/api/overview/azure/security.keyvault.secrets-readme)                                                                              | [![NuGet](https://img.shields.io/nuget/v/Spotflow.InMemory.Azure.KeyVault.svg)](https://www.nuget.org/packages/Spotflow.InMemory.Azure.KeyVault)                                       |

## Example

See how the in-memory Azure SDKs can be used in your code, for example with Azure Storage:

![Design](./docs/images/intro.excalidraw.svg)

With production code that looks like this:

```csharp
using Azure.Storage.Blobs;

public static class ExampleClass
{
    public static async Task PrintBlobAsync(BlobContainerClient container)
    {
        var blob = container.GetBlobClient("my-blob");
    
        var response = await blob.DownloadContentAsync();
    
        Console.WriteLine(response.Value.Content.ToString());
    }
}
```

the associated test code can be as simple as following:

```csharp
using Spotflow.InMemory.Azure.Storage;
using Spotflow.InMemory.Azure.Storage.Blobs;

var storageAccount = new InMemoryStorageProvider().AddAccount();

// The InMemoryBlobContainerClient inherits from BlobContainerClient (from the official SDK)
// So it can be used as a drop-in replacement for the real BlobContainerClient in your tests
var containerClient = InMemoryBlobContainerClient.FromAccount(storageAccount, "test-container");

// From now on, you can use the BlobContainerClient methods as you're used to:
containerClient.Create();

await containerClient.UploadBlobAsync("my-blob", BinaryData.FromString("Hello World!"));

// The `containerClient` can now be used in your code as if it was a real BlobContainerClient:

await ExampleClass.PrintBlobAsync(containerClient); // Output: Hello World!
```

This design allows you to create a factory for SDK clients with two implementations: one that provides the official Azure SDK clients and another that provides in-memory clients.
By selecting the appropriate factory, you can use the real implementation in your production code and the in-memory implementation in your tests.

You can learn how we recommend to use this library in [the documentations for each SDK](#supported-sdks).

## Key Features

-   **Drop-in Replacement** of the official Azure SDKs.
-   **Blazing-fast** thanks to the in-memory implementation.
-   **No external dependencies**, not even Docker.
-   **Fault Injection**: build resilient code thanks to simulated Azure outages in your tests.
-   **Delay Injection**: Examine behavior of your system under pressure of slowed-down operations.
-   **`TimeProvider` Support**: avoid flaky tests thanks to the time abstraction.
-   **Fluent Assertions** to conveniently test common scenarios.
-   **Customizable**: you can easily extend the functionality of the in-memory providers via [before and after hooks](./docs/hooks.md).

## Why Should I Use It?

There's been a lot written on why to prefer fakes over mocks in tests.
Mocks are test-doubles that return pre-programmed responses to method calls.
This can tightly couple your tests to implementation details, making them brittle and hard to maintain.
Fakes, on the other hand, are lightweight implementations of real services that can seamlessly integrate into your tests.
Using real services in tests is another approach, which is reasonable in many cases but can result in tests that are slow and harder to manage.

**One major drawback of fakes is the initial effort required to create them.
We have solved this problem by implementing them for you.**
This way, you can use the same interfaces and methods as in the real SDKs, but with the benefits of in-memory implementation.

## How It Works

The Azure SDKs are [designed](https://learn.microsoft.com/en-us/dotnet/azure/sdk/unit-testing-mocking?tabs=csharp) for inheritance-based testability:

-   Important methods are `virtual`.
-   There are parameterless protected constructor available for all clients.
-   There are static factories for creating most models.

The in-memory clients (e.g. `InMemoryBlobContainerClient` or `InMemoryEventHubConsumerClient`) provided by this library are inheriting the Azure SDK clients so that they can be injected to any code that expected the actual Azure SDK clients (the `BlobContainerClient` or `EventHubConsumerClient` the previous example). The tested code can therefore depend directly on Azure SDK clients and only abstract away creation of these clients. This removes the need to design and implement custom client interfaces.

The in-memory clients have similar constructors as real clients but they all also require a so-called in-memory provider (e.g. `InMemoryStorageProvider` or `InMemoryEventHubProvider`). The in-memory providers emulate the functionality of the actual services for which the SDK clients are created for (e.g. Azure Storage or Azure Event Hubs). The providers allows to read, change and assert the internal state during testing. For most Azure SDK clients, the in-memory providers are exposing corresponding types representing actual state. For example for `InMemoryBlobContainerClient: BlobContainerClient`, there is `InMemoryBlobContainer` type exposed by the provider. The important difference is that the `InMemoryBlobContainer` is representing the actual state (in this case an existing Azure Storage Blob Container) while `InMemoryBlobContainerClient` might be representing container that does not yet exist.

## Maintainers

-   [Tomáš Pajurek](https://github.com/tomas-pajurek) ([Spotflow](https://spotflow.io))
-   [David Nepožitek](https://github.com/DavidNepozitek) ([Spotflow](https://spotflow.io))

## Contributing

Please read our [Contributing Guidelines](./CONTRIBUTING.md) to learn how you can contribute to this project.

## License

This project is licensed under the [MIT license](./LICENSE.md).
