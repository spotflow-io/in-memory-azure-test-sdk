<h1 align="center">Azure Event Hubs</h1>

<p align="center">This library provides in-memory SDK for Azure Event Hubs which can be used as a drop-in replacement for the official 
<a href="https://www.nuget.org/packages/Azure.Messaging.EventHubs" target="_blank">Azure.Messaging.EventHubs SDK</a> in your tests.</p>

<p align="center">
    <a href="#recommended-usage">Recommended Usage</a> |
    <a href="#fault-injection">Fault Injection</a> |
    <a href="#features">Features</a> |
    <a href="#available-client-apis">Available APIs</a>
</p>

> [!TIP]
> See the whole [In-Memory Azure Test SDK](../README.md) suite if you are interested in other Azure services.

## Recommended Usage

To get started, add `Spotflow.InMemory.Azure.EventHubs` package to your project.

```shell
dotnet add Spotflow.InMemory.Azure.EventHubs
```

Create non-static factory class for creating the real Azure SDK clients. Relevant methods should be virtual to allow overriding as well as there should be a protected parameterless constructor for testing purposes.

```cs
class AzureClientFactory(TokenCredential tokenCredential)
{
    protected AzureClientFactory(): this(null!) {} // Testing-purposes only

    public virtual EventHubProducerClient CreateProducerClient(string fullyQualifiedNamespace, string eventHubName)
    {
        return new EventHubProducerClient(fullyQualifiedNamespace, eventHubName, tokenCredential);
    }
}
```

Use this class to obtain EventHub clients in the tested code:

```cs
class ExampleService(AzureClientFactory clientFactory, string fullyQualifiedNamespace, string eventHubName)
{
    private readonly EventHubProducerClient _client = clientFactory.CreateProducerClient(fullyQualifiedNamespace, eventHubName);

    public async Task SendEventAsync(BinaryData payload)
    {
        await _client.SendAsync(new EventData(payload));
    }
}
```

Create `InMemoryAzureClientFactory` by inheriting `AzureClientFactory` and override relevant factory methods to return in-memory clients:

```cs
class InMemoryAzureClientFactory(InMemoryEventHubProvider provider): AzureClientFactory
{
    public override EventHubProducerClient CreateProducerClient(string fullyQualifiedNamespace, string eventHubName)
    {
        return new InMemoryEventHubProducerClient(fullyQualifiedNamespace, eventHubName, NoOpTokenCredential.Instance, provider);
    }
}
```

When testing, it is now enough to initialize `InMemoryEventHubProvider` and inject `InMemoryAzureClientFactory` to the tested code (e.g. via Dependency Injection):

```cs
var provider = new InMemoryEventHubProvider();
var eventHub = provider.AddNamespace().AddEventHub("test-event-hub", numberOfPartitions: 4);

var services = new ServiceCollection();

services.AddSingleton<ExampleService>();
services.AddSingleton(provider);
services.AddSingleton<AzureClientFactory, InMemoryAzureClientFactory>();

var exampleService = services.BuildServiceProvider().GetRequiredService<ExampleService>();

var payload = BinaryData.FromString("test-data");

await exampleService.SendEventAsync(eventHub.Namespace.Hostname, eventHub.Name, payload);

var receiver = InMemoryPartitionReceiver.FromEventHub(partitionId: "0", eventHub);

var batch = await receiver.ReceiveBatchAsync(100, TimeSpan.Zero);

batch.Should().ContainSingle(e => e.EventBody.ToString() == "test-data");
```

## Fault Injection

Fault injections let you simulate transient and persistent faults in Azure Event Hub.
Thanks to that you can test how your application behaves in case of Azure outages, network issues, timeouts, etc.

To inject a fault, you need to use the [concept of hooks](./hooks.md) - functions that are called before or after the actual operation is executed.
A new hook can be registered by calling the `AddHook` method on the `InMemoryEventHubProvider` instance.
You can build fault hook by calling the `Faults` method on the hook context and then calling the appropriate method, e.g. `ServiceIsBusy`:

For overview of available hooks, please see the [Hooks](#hooks) section.

```cs
var provider = new InMemoryEventHubProvider();
var hook = provider.AddHook(hookBuilder => hookBuilder.Before(ctx => ctx.Faults().ServiceIsBusy()));
```

The `AddHook` method gives you a builder that lets you define which operations the hook should apply to.
In the example above, the hook affects all Event Hub operations.
However, you can limit it to specific operations, like `Send`, or target specific scopes, such as operation on Event Hub called `my-eventhub`:

```cs
var hook = provider.AddHook(hookBuilder => hookBuilder.ForProducer(eventHubName: "my-event-hub")
    .BeforeSend(ctx => ctx.Faults().ServiceIsBusy()));
```

You can control when the hook should execute via the `IHookRegistration` interface returned by the `AddHook` method.
By default, the hook is enabled, but you can disable it by calling the `Disable` method.
To simulate temporary outages, use the `DisableAfter` method to limit the number of fault occurrences.

See a full example of fault injection below:

```cs
var provider = new InMemoryEventHubProvider();

var hook = provider.AddHook(hook => hook.Before(ctx => ctx.Faults().ServiceIsBusy()));

var eventHub = provider.AddNamespace("test-ns").AddEventHub("test-eh", 1);

var producerClient = InMemoryEventHubProducerClient.FromEventHub(eventHub);

var act = () => producerClient.SendAsync([new EventData()]);

await act.Should().ThrowAsync<EventHubsException>().WithMessage("Event hub 'test-eh' in namespace 'test-ns' is busy. (test-eh). *");

hook.Disable();

await act.Should().NotThrowAsync();
```

## Delay Simulation

Delay simulation is currently not supported for Azure Event Hub.

However, [hooks](hooks.md) can be used to simulate custom delays. For overview of available hooks, please see the [Hooks](#hooks) section.

## Supported APIs and features

### SDK clients & methods

Following SDK clients and their method groups and properties are supported.

Async versions of these methods are also supported. All supported async methods starts with [Task.Yield()](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.yield) to force the method to complete asynchronously.

Other methods and properties are not supported and will throw `NotSupportedException`.

Clients are thread-safe.

#### `InMemoryEventHubProducerClient : EventHubProducerClient`

| Property                  | Note |
| ------------------------- | ---- |
| `EventHubName`            |      |
| `FullyQualifiedNamespace` |      |
| `Identifier`              |      |
| `IsClosed`                |      |

| Method group                  |
| ----------------------------- |
| `CloseAsync`                  |
| `CreateBatchAsync`            |
| `DisposeAsync`                |
| `CreateBatchAsync`            |
| `GetEventHubPropertiesAsync`  |
| `GetPartitionIdsAsync`        |
| `GetPartitionPropertiesAsync` |

| Constructors & factory methods                                                           | Note |
| ---------------------------------------------------------------------------------------- | ---- |
| `(string connectionString)`                                                              |      |
| `(string fullyQualifiedNamespace, string eventHubName, TokenCredential tokenCredential)` |      |
| `(EventHubConnection connection)`                                                        |      |
| `FromEventHub(InMemoryEventHub eventHub)`                                                |      |
| `FromNamespace(InMemoryEventHubNamespace eventHubNamespace, string eventHubName)`        |      |

#### `InMemoryEventHubConsumerClient : EventHubConsumerClient`

| Property                  | Note |
| ------------------------- | ---- |
| `ConsumerGroup`           |      |
| `EventHubName`            |      |
| `FullyQualifiedNamespace` |      |
| `Identifier`              |      |
| `IsClosed`                |      |

| Method group                  |
| ----------------------------- |
| `CloseAsync`                  |
| `DisposeAsync`                |
| `GetEventHubPropertiesAsync`  |
| `GetPartitionIdsAsync`        |
| `GetPartitionPropertiesAsync` |

| Constructors & factory methods                                                                                 | Note |
| -------------------------------------------------------------------------------------------------------------- | ---- |
| `(string consumerGroup, string connectionString)`                                                              |      |
| `(string consumerGroup, string fullyQualifiedNamespace, string eventHubName, TokenCredential tokenCredential)` |      |
| `(string consumerGroup, EventHubConnection connection)`                                                        |      |
| `FromEventHub(string consumerGroup, InMemoryEventHub eventHub)`                                                |      |
| `FromNamespace(string consumerGroup, InMemoryEventHubNamespace eventHubNamespace, string eventHubName)`        |      |

#### `InMemoryPartitionReceiver : PartitionReceiver`

| Property                  | Note |
| ------------------------- | ---- |
| `ConsumerGroup`           |      |
| `EventHubName`            |      |
| `FullyQualifiedNamespace` |      |
| `Identifier`              |      |
| `InitialPosition`         |      |
| `IsClosed`                |      |
| `PartitionId`             |      |

| Method group                      |
| --------------------------------- |
| `GetPartitionPropertiesAsync`     |
| `ReadLastEnqueuedEventProperties` |
| `ReceiveBatchAsync`               |
| `DisposeAsync`                    |
| `CloseAsync`                      |

| Constructors & factory methods                                                                                                                              | Note                                                          |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------- |
| `(string consumerGroup, string partitionId, EventPosition eventPosition, string connectionString)`                                                          | No credentials are validated.                                 |
| `(string consumerGroup, string partitionId, EventPosition eventPosition, string connectionString, string eventHubName)`                                     | No credentials are validated.                                 |
| `(string consumerGroup, string partitionId, EventPosition eventPosition, string fullyQualifiedNamespace, string eventHubName, TokenCredential credential)`  | No credentials are validated.                                 |
| `(string consumerGroup, string partitionId, EventPosition eventPosition, EventHubConnection connection)`                                                    | No credentials are validated.                                 |
| `FromNamespace(string consumerGroup, string partitionId, EventPosition startingPosition, InMemoryEventHubNamespace eventHubNamespace, string eventHubName)` | No credentials are validated.                                 |
| `FromEventHub(string consumerGroup, string partitionId, EventPosition startingPosition, InMemoryEventHub eventHub)`                                         | No credentials are validated.                                 |
| `FromEventHub(string partitionId, EventPosition startingPosition, InMemoryEventHub eventHub)`                                                               | Default consumer group is used. No credentials are validated. |

### Features

For the supported methods enumerated above, not all features are fully implemented.

If the given feature is not supported, than the method will just ignore any parameters related to that feature.

| Feature                                                                      | Is Supported |
| ---------------------------------------------------------------------------- | ------------ |
| Batches                                                                      | ✅           |
| Event System Property - Content Type                                         | ✅           |
| Event System Property - Correlation Id                                       | ✅           |
| Event System Property - Message Id                                           | ✅           |
| Event System Property - Offset (\*)                                          | ✅           |
| Event System Property - Sequence Number                                      | ✅           |
| Offset-based starting positions                                              | ❌           |
| Partition keys                                                               | ✅           |
| Properties - Event Hub                                                       | ✅           |
| Properties - Partition                                                       | ✅           |
| Randomization of initial sequence numbers for event hub partitions           | ✅           |
| Sequence number based starting positions (including `Earliest` and `Latest`) | ✅           |

(\*) The value of `Offset` system property is based on event data size. Calculation of the the event data size is not exactly the same as for real Azure Event Hubs but should be good enough for testing purposes. 

## Hooks

Following hooks are supported in both `Before` and `After` variants:

-   All `Event Hub` operations
    -   All `Producer` operations
        -   `Send`
    -   All `Consumer` operations
        -   `ReceiveBatch`
        -   `GetEventHubProperties`
        -   `GetPartitionProperties`

For details about concept of hooks, please see the [Hooks](./hooks.md) page.
