<h1 align="center">Azure Service Bus</h1>

<p align="center">This library provides in-memory SDK for Azure Service Bus which can be used as a drop-in replacement for the official 
<a href="https://www.nuget.org/packages/Azure.Messaging.ServiceBus" target="_blank">Azure.Messaging.ServiceBus</a> in your tests.</p>

<p align="center">
    <a href="#example-usage">Example Usage</a> |
    <a href="#fault-injection">Fault Injection</a> |
    <a href="#features">Features</a> |
    <a href="#available-client-apis">Available APIs</a> |
    <a href="#available-fluent-assertions">Fluent Assertions</a>
</p>

> [!TIP]
> See the whole [In-Memory Azure Test SDK](../README.md) suite if you are interested in other Azure services.

## Example Usage

To get started, add `Spotflow.InMemory.Azure.ServiceBus` package to your project.

```shell
dotnet add Spotflow.InMemory.Azure.ServiceBus
```
This package provides in-memory implementation of Azure Service Bus SDK clients and models.
These in-memory implementations are inheriting the real Azure SDK types so you can use them as a drop-in replacement in your tests.
There is nothing special about the in-memory types, so they can be injected in many ways, e.g. via DI and constructor injection as demonstrated below.
Only extra step is to create parent `InMemoryServiceBusProvider` instance for the in-memory clients.

Let's consider the following type `ExampleService` as an example:

```cs
class ExampleService(InMemoryServiceBusClient client)
{
    private readonly ServiceBusSender _sender = client.CreateSender("test-queue");

    public async Task SendMessageAsync(BinaryData payload)
    {
        await _sender.SendMessageAsync(new ServiceBusMessage(payload));
    }
}
```

The `ExampleService` might be constructed, for example, using DI:

```cs
// Setup DI - production configuration
var connectionString = "Endpoint=sb://test-namespace...;";

var services = new ServiceCollection();

services.AddSingleton<ServiceBusClient>(new ServiceBusClient(connectionString));
services.AddSingleton<ExampleService>();
...

// Use resulting service provider
var service = services.BuildServiceProvider().GetRequiredService<ExampleService>();
```

*Note:
Most frequently, the `new ServiceCollection()` and `.BuildServiceProvider()` will called by ASP.NET or other frameworks.
This is just an example of one of many ways how the in-memory clients can be used.*


To inject the in-memory implementation of `ServiceBusClient` to the `ExampleService` during test,
the `InMemoryServiceBusClient` can be simply substituted for the real `ServiceBusClient` in the DI container:

```csharp
// Setup DI - test-only configuration (additive)
var inMemoryProvider = new InMemoryServiceBusProvider();
var client = new InMemoryServiceBusClient(connectionString, inMemoryProvider);

services.AddSingleton<ServiceBusClient>(client);
```

By default, the `InMemoryServiceBusProvider` is empty but exposes methods that allow to set up expected management-plane state:

```csharp
inMemoryProvider.AddNamespace("test-namespace...").AddQueue("test-queue", new() { EnableSessions = true });
```

To send/receive messages in test code (data-plane operations), the in-memory clients can be used directly:

```cs
await using var client = new InMemoryServiceBusClient(connectionString, inMemoryProvider);

await using var sender = client.CreateSender("test-queue");

await using var receiver = client.CreateReceiver("test-queue");

await sender.SendMessageAsync(new ServiceBusMessage(...));

var message = await receiver.ReceiveMessageAsync();
```

## Fault Injection

Fault injection is currently not supported for Azure Service Bus.

However, [hooks](hooks.md) can be used to simulate custom faults. For overview of available hooks, please see the [Hooks](#hooks) section.

## Delay Simulation

Delay simulation is currently not supported for Azure Service Bus.

However, [hooks](hooks.md) can be used to simulate custom delays. For overview of available hooks, please see the [Hooks](#hooks) section.

## Supported APIs and features

### SDK clients & methods

Following SDK clients and their method groups and properties are supported.

Async versions of these methods are also supported. All supported async methods are guaranteed executed truly asynchronously by using [Task.Yield()](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.yield).


Other methods and properties are not supported and will throw `NotSupportedException`.

Clients are thread-safe.

#### `InMemoryServiceBusClient: ServiceBusClient`

| Property                  | Note |
| ------------------------- | ---- |
| `FullyQualifiedNamespace` |      |
| `Identifier`              |      |
| `IsClosed`                |      |
| `TransportType`           |      |

| Method group             |
| ------------------------ |
| `AcceptNextSessionAsync` |
| `AcceptSessionAsync`     |
| `CreateSender`           |
| `CreateReceiver`         |
| `DisposeAsync`           |

| Constructors & factory methods                                                                            | Note                          |
| --------------------------------------------------------------------------------------------------------- | ----------------------------- |
| `(string connectionString)`                                                                               | No credentials are validated. |
| `(string connectionString, ServiceBusClientOptions options)`                                              | No credentials are validated. |
| `(string fullyQualifiedNamespace, TokenCredential credential)`                                            | No credentials are validated. |
| `(string fullyQualifiedNamespace, TokenCredential credential, ServiceBusClientOptions options)`           | No credentials are validated. |
| `FromNamespace(InMemoryServiceBusNamespace serviceBusNamespace, ServiceBusClientOptions? options = null)` |                               |

#### `InMemoryServiceBusSender : ServiceBusSender`

| Property                  | Note |
| ------------------------- | ---- |
| `EntityPath`              |      |
| `FullyQualifiedNamespace` |      |
| `Identifier`              |      |
| `IsClosed`                |      |

| Method group              |
| ------------------------- |
| `SendMessageAsync`        |
| `SendMessagesAsync`       |
| `CreateMessageBatchAsync` |
| `DisposeAsync`            |
| `CloseAsync`              |

| Constructors & factory methods                                                                | Note                          |
| --------------------------------------------------------------------------------------------- | ----------------------------- |
| `(InMemoryServiceBusClient client, string queueOrTopicName)`                                  | No credentials are validated. |
| `(InMemoryServiceBusClient client, string queueOrTopicName, ServiceBusSenderOptions options)` | No credentials are validated. |
| `FromQueue(InMemoryServiceBusQueue queue, ServiceBusClientOptions? options = null)`           |                               |
| `FromTopic(InMemoryServiceBusTopic topic, ServiceBusClientOptions? options = null)`           |                               |

#### `InMemoryServiceBusReceiver: ServiceBusReceiver`

| Property                  | Note |
| ------------------------- | ---- |
| `EntityPath`              |      |
| `FullyQualifiedNamespace` |      |
| `Identifier`              |      |
| `IsClosed`                |      |
| `PrefetchCount`           |      |
| `ReceiveMode`             |      |

| Method group            |
| ----------------------- |
| `AbandonMessageAsync`   |
| `CloseAsync`            |
| `CompleteMessageAsync`  |
| `DisposeAsync`          |
| `ReceiveMessageAsync`   |
| `ReceiveMessagesAsync`  |
| `RenewMessageLockAsync` |

| Constructors & factory methods                                                                                  | Note                          |
| --------------------------------------------------------------------------------------------------------------- | ----------------------------- |
| `(InMemoryServiceBusClient client, string queueName)`                                                           | No credentials are validated. |
| `(InMemoryServiceBusClient client, string queueName, ServiceBusSenderOptions options)`                          | No credentials are validated. |
| `(InMemoryServiceBusClient client, string queueName, string subscriptionName)`                                  | No credentials are validated. |
| `(InMemoryServiceBusClient client, string queueName, string subscriptionName, ServiceBusSenderOptions options)` | No credentials are validated. |
| `FromQueue(InMemoryServiceBusQueue queue, ServiceBusClientOptions? options = null)`                             |                               |
| `FromSubscription(InMemoryServiceBusTopicSubscription subscription, ServiceBusClientOptions? options = null)`   |                               |

#### `InMemoryServiceBusSessionReceiver : ServiceBusSessionReceiver`

| Property                  | Note |
| ------------------------- | ---- |
| `EntityPath`              |      |
| `FullyQualifiedNamespace` |      |
| `Identifier`              |      |
| `IsClosed`                |      |
| `PrefetchCount`           |      |
| `ReceiveMode`             |      |
| `SessionId`               |      |
| `SessionLockedUntil`      |      |

| Method group            |
| ----------------------- |
| `AbandonMessageAsync`   |
| `CloseAsync`            |
| `CompleteMessageAsync`  |
| `DisposeAsync`          |
| `GetSessionStateAsync`  |
| `ReceiveMessageAsync`   |
| `ReceiveMessagesAsync`  |
| `RenewMessageLockAsync` |
| `RenewSessionLockAsync` |
| `SetSessionStateAsync`  |

No public constructors are available.

### Features

For the supported methods enumerated above, not all features are fully implemented.

If the given feature is not supported, than the method will just ignore any parameters related to that feature.

| Feature                         | Is Supported |
| ------------------------------- | ------------ |
| Deferred messages               | ❌           |
| Dead-letter queues              | ❌           |
| Duplicate detection             | ❌           |
| Partitioning                    | ❌           |
| `PeekLock` receive mode         | ✅           |
| Processors                      | ❌           |
| Queues                          | ✅           |
| `ReceiveAndDelete` receive mode | ✅           |
| Rules                           | ❌           |
| Scheduled messages              | ❌           |
| Sessions                        | ✅           |
| Session states                  | ✅           |
| Sequence numbers                | ✅           |
| Subscriptions                   | ✅           |
| Topics                          | ✅           |

## Available Fluent Assertions

There are following assertions available for in-memory service bus types:

### `InMemoryServiceBusQueue`

-   `.Should().BeEmptyAsync()`

### `InMemoryServiceBusTopicSubscription`

-   `.Should().BeEmptyAsync()`

## Hooks

Following hooks are supported in both `Before` and `After` variants:

-   All `Service Bus` operations
    -   All `Producer` operations
        -   `SendMessage`
        -   `SendBatch`
    -   All `Consumer` operations
        -   `ReceiveMessage`
        -   `ReceiveBatch`

For details about concept of hooks, please see the [Hooks](./hooks.md) page.
