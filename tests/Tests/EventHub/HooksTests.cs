using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;

using Spotflow.InMemory.Azure.EventHubs;
using Spotflow.InMemory.Azure.EventHubs.Hooks;
using Spotflow.InMemory.Azure.EventHubs.Hooks.Contexts;
using Spotflow.InMemory.Azure.Hooks;

using Tests.Utils;

namespace Tests.EventHub;

[TestClass]
public class HooksTests
{

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Send_Hooks_Should_Execute()
    {
        const string namespaceName = "test-ns";
        const string eventHubName = "test-eh";

        var provider = new InMemoryEventHubProvider();

        var eventHub = provider.AddNamespace(namespaceName).AddEventHub(eventHubName, 2);

        var connectionString = eventHub.CreateConnectionString();

        await using var client = new InMemoryEventHubProducerClient(connectionString, provider);

        SendBeforeHookContext? capturedBeforeContext = null;
        SendAfterHookContext? capturedAfterContext = null;

        provider.AddHook(builder => builder.ForProducer().BeforeSend(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }));

        provider.AddHook(builder => builder.ForProducer().AfterSend(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }));

        var eventData = new EventData { MessageId = "test-mi", ContentType = "test-ct", CorrelationId = "test-ci" };

        await client.SendAsync([eventData]);

        var scope = new EventHubScope(namespaceName, eventHubName);

        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext?.Operation.Should().Be(ProducerOperations.Send);
        capturedBeforeContext?.EventBatch.Should().HaveCount(1);
        capturedBeforeContext?.EventBatch[0].Should().BeEquivalentTo(eventData);

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.Operation.Should().Be(ProducerOperations.Send);
        capturedAfterContext?.EventBatch.Should().HaveCount(1);
        capturedAfterContext?.EventBatch[0].Should().BeEquivalentTo(eventData);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Receive_Batch_Hooks_Should_Execute()
    {
        const string namespaceName = "test-ns";
        const string eventHubName = "test-eh";

        var provider = new InMemoryEventHubProvider();

        var eventHub = provider.AddNamespace(namespaceName).AddEventHub(eventHubName, 1);

        var connectionString = eventHub.CreateConnectionString();

        await using var producerClient = new InMemoryEventHubProducerClient(connectionString, provider);
        await using var consumerClient = InMemoryPartitionReceiver.FromEventHub("0", EventPosition.Earliest, eventHub);

        ReceiveBatchBeforeHookContext? capturedBeforeContext = null;
        ReceiveBatchAfterHookContext? capturedAfterContext = null;

        provider.AddHook(builder => builder.ForConsumer().BeforeReceiveBatch(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }));

        provider.AddHook(builder => builder.ForConsumer().AfterReceiveBatch(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }));

        var eventData = new EventData { MessageId = "test-mi", ContentType = "test-ct", CorrelationId = "test-ci", Data = BinaryData.FromString("test-data") };

        await producerClient.SendAsync([eventData]);

        await consumerClient.ReceiveBatchAsync(10);

        var scope = new EventHubScope(namespaceName, eventHubName);

        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext?.Operation.Should().Be(ConsumerOperations.ReceiveBatch);

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.Operation.Should().Be(ConsumerOperations.ReceiveBatch);
        capturedAfterContext?.EventBatch.Should().HaveCount(1);
        capturedAfterContext?.EventBatch[0].Data?.ToString().Should().BeEquivalentTo(eventData.Data.ToString());
        capturedAfterContext?.EventBatch[0].Properties.Should().BeEquivalentTo(eventData.Properties);
    }

    [TestMethod]
    public async Task GetEventHubProperties_Hooks_For_Consumer_Should_Execute()
    {
        EventHubBeforeHookContext? capturedBeforeContextGeneric = null;
        GetConsumerEventHubPropertiesBeforeHookContext? capturedBeforeContextSpecific = null;
        EventHubAfterHookContext? capturedAfterContextGeneric = null;
        GetConsumerEventHubPropertiesAfterHookContext? capturedAfterContextSpecific = null;

        var provider = new InMemoryEventHubProvider();

        provider.AddHook(hook => hook.Before(ctx => { capturedBeforeContextGeneric = ctx; return Task.CompletedTask; }));
        provider.AddHook(hook => hook.ForConsumer().BeforeGetEventHubProperties(ctx => { capturedBeforeContextSpecific = ctx; return Task.CompletedTask; }));
        provider.AddHook(hook => hook.ForConsumer().AfterGetEventHubProperties(ctx => { capturedAfterContextSpecific = ctx; return Task.CompletedTask; }));
        provider.AddHook(hook => hook.After(ctx => { capturedAfterContextGeneric = ctx; return Task.CompletedTask; }));

        var eventHub = provider.AddNamespace().AddEventHub("test", 1);

        var client = InMemoryEventHubConsumerClient.FromEventHub("$default", eventHub);

        await client.GetEventHubPropertiesAsync();

        capturedBeforeContextSpecific.Should().NotBeNull();
        capturedBeforeContextGeneric.Should().BeOfType<GetConsumerEventHubPropertiesBeforeHookContext>();

        capturedAfterContextSpecific.Should().NotBeNull();
        capturedAfterContextGeneric.Should()
            .BeOfType<GetConsumerEventHubPropertiesAfterHookContext>()
            .Which.EventHubProperties.PartitionIds.Should().HaveCount(1);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Hooks_With_Different_Scope_Should_Not_Execute()
    {
        const string namespaceName = "test-ns";
        const string eventHubName = "test-eh";

        var provider = new InMemoryEventHubProvider();

        var eventHub = provider.AddNamespace(namespaceName).AddEventHub(eventHubName, 2);

        var connectionString = eventHub.CreateConnectionString();

        await using var client = new InMemoryEventHubProducerClient(connectionString, provider);

        HookFunc<SendBeforeHookContext> failingBeforeHook = _ => throw new InvalidOperationException("This hook should not execute.");
        HookFunc<SendAfterHookContext> failingAfterHook = _ => throw new InvalidOperationException("This hook should not execute.");

        provider.AddHook(builder => builder.ForProducer(eventHubNamespaceName: "different").BeforeSend(failingBeforeHook));
        provider.AddHook(builder => builder.ForProducer(eventHubName: "different").BeforeSend(failingBeforeHook));


        provider.AddHook(builder => builder.ForProducer(eventHubNamespaceName: "different").AfterSend(failingAfterHook));
        provider.AddHook(builder => builder.ForProducer(eventHubName: "different").AfterSend(failingAfterHook));


        var eventData = new EventData { MessageId = "test-mi", ContentType = "test-ct", CorrelationId = "test-ci" };

        await client.SendAsync([eventData]);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Parent_Hook_Should_Execute()
    {
        const string namespaceName = "test-ns";
        const string eventHubName = "test-eh";

        var provider = new InMemoryEventHubProvider();

        var eventHub = provider.AddNamespace(namespaceName).AddEventHub(eventHubName, 2);

        var connectionString = eventHub.CreateConnectionString();

        await using var client = new InMemoryEventHubProducerClient(connectionString, provider);

        EventHubBeforeHookContext? capturedBeforeContext = null;
        EventHubAfterHookContext? capturedAfterContext = null;

        provider.AddHook(builder => builder.Before(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }));

        provider.AddHook(builder => builder.After(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }));

        var eventData = new EventData { MessageId = "test-mi", ContentType = "test-ct", CorrelationId = "test-ci" };

        await client.SendAsync([eventData]);

        var scope = new EventHubScope(namespaceName, eventHubName);

        capturedBeforeContext.Should().NotBeNull();

        capturedBeforeContext.Should().BeOfType<SendBeforeHookContext>();

        ((SendBeforeHookContext) capturedBeforeContext!).Operation.Should().Be(ProducerOperations.Send);

        capturedAfterContext.Should().NotBeNull();

        capturedAfterContext.Should().BeOfType<SendAfterHookContext>();
        ((SendAfterHookContext) capturedAfterContext!).Operation.Should().Be(ProducerOperations.Send);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Targeted_Hooks_Should_Execute()
    {
        const string namespaceName = "test-ns";
        const string eventHubName = "test-eh";

        var provider = new InMemoryEventHubProvider();

        var eventHub = provider.AddNamespace(namespaceName).AddEventHub(eventHubName, 2);

        var connectionString = eventHub.CreateConnectionString();

        await using var client = new InMemoryEventHubProducerClient(connectionString, provider);

        ProducerBeforeHookContext? capturedBeforeContext = null;
        ProducerAfterHookContext? capturedAfterContext = null;

        provider.AddHook(builder => builder.ForProducer().Before(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }, ProducerOperations.Send));

        provider.AddHook(builder => builder.ForProducer().After(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }, ProducerOperations.Send));

        var eventData = new EventData { MessageId = "test-mi", ContentType = "test-ct", CorrelationId = "test-ci" };

        await client.SendAsync([eventData]);

        var scope = new EventHubScope(namespaceName, eventHubName);

        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext?.Operation.Should().Be(ProducerOperations.Send);

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.Operation.Should().Be(ProducerOperations.Send);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Hooks_With_Different_Target_Should_Not_Execute()
    {
        const string namespaceName = "test-ns";
        const string eventHubName = "test-eh";

        var provider = new InMemoryEventHubProvider();

        var eventHub = provider.AddNamespace(namespaceName).AddEventHub(eventHubName, 2);

        var connectionString = eventHub.CreateConnectionString();

        await using var client = new InMemoryEventHubProducerClient(connectionString, provider);

        provider.AddHook(
            builder => builder.ForConsumer().Before(_ => throw new InvalidOperationException("This hook should not execute."), ConsumerOperations.ReceiveBatch));

        provider.AddHook(
            builder => builder.ForConsumer().After(_ => throw new InvalidOperationException("This hook should not execute."), ConsumerOperations.ReceiveBatch));

        var eventData = new EventData { MessageId = "test-mi", ContentType = "test-ct", CorrelationId = "test-ci" };

        await client.SendAsync([eventData]);
    }
}
