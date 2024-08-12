<h1 align="center">Azure Service Bus</h1>

<p align="center">This library provides in-memory SDK for Azure Event Hubs which can be used as a drop-in replacement for the official 
<a href="https://www.nuget.org/packages/Azure.Messaging.ServiceBus" target="_blank">Azure.Messaging.ServiceBus</a> in your tests.</p>

<p align="center">
    <a href="#recommended-usage">Recommended Usage</a> |
    <a href="#fault-injection">Fault Injection</a> |
    <a href="#features">Features</a> |
    <a href="#available-client-apis">Available APIs</a> |
    <a href="#available-fluent-assertions">Fluent Assertions</a>
</p>

> [!TIP]
> See the whole [In-Memory Azure Test SDK](../README.md) suite if you are interested in other Azure services.

## Recommended Usage

To get started, add `Spotflow.InMemory.Azure.EventHubs` package to your project.

```shell
dotnet add Spotflow.InMemory.Azure.ServiceBus
```

Create non-static factory class for creating the real Azure SDK clients. Relevant methods should be virtual to allow overriding as well as there should be a protected parameterless constructor for testing purposes.

```cs
class AzureClientFactory(TokenCredential tokenCredential)
{
    protected AzureClientFactory(): this(null!) {} // Testing-purposes only

    public virtual ServiceBusSender CreateServiceBusSender(string fullyQualifiedNamespace, string queueOrTopicName)
    {
        return new ServiceBusClient(fullyQualifiedNamespace, tokenCredential).CreateSender(queueOrTopicName);
    }
}
```

Use this class to obtain ServiceBus clients in the tested code:

```cs
class ExampleService(AzureClientFactory clientFactory, string fullyQualifiedNamespace, string queueName)
{
    private readonly ServiceBusSender _client  = clientFactory.CreateServiceBusSender(fullyQualifiedNamespace, queueName);

    public async Task SendMessageAsync(BinaryData payload)
    {
        await _client.SendMessageAsync(new ServiceBusMessage(payload));
    }
}
```

Create `InMemoryAzureClientFactory` by inheriting `AzureClientFactory` and override relevant factory methods to return in-memory clients:

```cs
class InMemoryAzureClientFactory(InMemoryServiceBusProvider provider) : AzureClientFactory
{
    public override ServiceBusSender CreateServiceBusSender(string fullyQualifiedNamespace, string queueOrTopicName)
    {
        return new InMemoryServiceBusClient(fullyQualifiedNamespace, NoOpTokenCredential.Instance, provider).CreateSender(queueOrTopicName);
    }
}
```

When testing, it is now enough to initialize `InMemoryServiceBusProvider` and inject `InMemoryAzureClientFactory` to the tested code (e.g. via Dependency Injection):

```csharp
var provider = new InMemoryServiceBusProvider();
var queue = serviceBusProvider.AddNamespace().AddQueue("my-queue");

var services = new ServiceCollection();

services.AddSingleton<ExampleService>();
services.AddSingleton(provider);
services.AddSingleton<AzureClientFactory, InMemoryAzureClientFactory>();

var exampleService = services.BuildServiceProvider().GetRequiredService<ExampleService>();

var payload = BinaryData.FromString("test-data");

await exampleService.SendMessageAsync(queue.Namespace.FullyQualifiedNamespace, queue.QueueName, payload);

await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace, provider);

await using var receiver = client.CreateReceiver(queue.QueueName);

var message = await receiver.ReceiveMessageAsync();

message.Body.ToString().Should().Be("test-data");

await receiver.CompleteMessageAsync(message);
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

Async versions of these methods are also supported. All supported async methods starts with [Task.Yield()](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.yield) to force the method to complete asynchronously.

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
