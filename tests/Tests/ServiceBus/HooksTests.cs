using Spotflow.InMemory.Azure.Hooks;
using Spotflow.InMemory.Azure.ServiceBus;
using Spotflow.InMemory.Azure.ServiceBus.Hooks;
using Spotflow.InMemory.Azure.ServiceBus.Hooks.Contexts;

using Tests.Utils;

namespace Tests.ServiceBus;

[TestClass]
public class HooksTests
{

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Send_Batch_Hooks_Should_Execute()
    {
        const string namespaceName = "test-namespace";
        const string queueName = "test-queue";

        var provider = new InMemoryServiceBusProvider();
        var ns = provider.AddNamespace(namespaceName);
        ns.AddQueue(queueName);

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var sender = client.CreateSender(queueName);

        SendBatchBeforeHookContext? capturedBeforeContext = null;
        SendBatchAfterHookContext? capturedAfterContext = null;

        provider.AddHook(builder => builder.ForProducer().BeforeSendBatch(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }));

        provider.AddHook(builder => builder.ForProducer().AfterSendBatch(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }));

        using var batch = await sender.CreateMessageBatchAsync();

        batch.TryAddMessage(new(BinaryData.FromString("Message 1")));

        await sender.SendMessagesAsync(batch);

        await using var receiver = client.CreateReceiver(queueName);

        await receiver.ReceiveMessagesAsync(3, TimeSpan.FromMilliseconds(100));

        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext?.Messages.Should().HaveCount(1);
        capturedBeforeContext?.Messages[0].Body.ToString().Should().Be("Message 1");

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.Messages.Should().HaveCount(1);
        capturedAfterContext?.Messages[0].Body.ToString().Should().Be("Message 1");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Receive_Batch_Hooks_Should_Execute()
    {
        const string namespaceName = "test-namespace";
        const string queueName = "test-queue";

        var provider = new InMemoryServiceBusProvider();
        var ns = provider.AddNamespace(namespaceName);
        ns.AddQueue(queueName);

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var sender = client.CreateSender(queueName);

        ReceiveBatchBeforeHookContext? capturedBeforeContext = null;
        ReceiveBatchAfterHookContext? capturedAfterContext = null;

        provider.AddHook(builder => builder.ForConsumer().AfterReceiveBatch(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }));

        provider.AddHook(builder => builder.ForConsumer().AfterReceiveBatch(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }));

        using var batch = await sender.CreateMessageBatchAsync();

        batch.TryAddMessage(new(BinaryData.FromString("Message 1")));

        await sender.SendMessagesAsync(batch);

        await using var receiver = client.CreateReceiver(queueName);

        await receiver.ReceiveMessagesAsync(3, TimeSpan.FromMilliseconds(100));

        capturedBeforeContext.Should().NotBeNull();

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.Messages.Should().HaveCount(1);
        capturedAfterContext?.Messages[0].Body.ToString().Should().Be("Message 1");
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Hooks_With_Different_Scope_Should_Not_Execute()
    {
        const string namespaceName = "test-namespace";
        const string queueName = "test-queue";

        var provider = new InMemoryServiceBusProvider();
        var ns = provider.AddNamespace(namespaceName);
        ns.AddQueue(queueName);

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var sender = client.CreateSender(queueName);

        HookFunc<ServiceBusBeforeHookContext> failingBeforeHook = _ => throw new InvalidOperationException("This hook should not execute");
        HookFunc<ServiceBusAfterHookContext> failingAfterHook = _ => throw new InvalidOperationException("This hook should not execute");

        provider.AddHook(builder => builder.ForProducer(serviceBusNamespaceName: "different").BeforeSendBatch(failingBeforeHook));
        provider.AddHook(builder => builder.ForProducer(entityPath: "different").BeforeSendBatch(failingBeforeHook));
        provider.AddHook(builder => builder.ForProducer(serviceBusNamespaceName: "different").AfterSendBatch(failingAfterHook));
        provider.AddHook(builder => builder.ForProducer(entityPath: "different").AfterSendBatch(failingAfterHook));

        using var batch = await sender.CreateMessageBatchAsync();

        batch.TryAddMessage(new(BinaryData.FromString("Message 1")));

        await sender.SendMessagesAsync(batch);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Parent_Hook_Should_Execute()
    {
        const string namespaceName = "test-namespace";
        const string queueName = "test-queue";

        var provider = new InMemoryServiceBusProvider();
        var ns = provider.AddNamespace(namespaceName);
        ns.AddQueue(queueName);

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var sender = client.CreateSender(queueName);

        ServiceBusBeforeHookContext? capturedBeforeContext = null;
        ServiceBusAfterHookContext? capturedAfterContext = null;

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

        using var batch = await sender.CreateMessageBatchAsync();

        batch.TryAddMessage(new(BinaryData.FromString("Message 1")));

        await sender.SendMessagesAsync(batch);

        capturedBeforeContext.Should().NotBeNull();
        capturedBeforeContext.Should().BeOfType<SendBatchBeforeHookContext>();

        ((SendBatchBeforeHookContext) capturedBeforeContext!).Operation.Should().Be(ProducerOperations.SendBatch);
        capturedBeforeContext.ServiceBusNamespaceName.Should().Be(namespaceName);
        capturedBeforeContext.EntityPath.Should().Be(queueName);

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext.Should().BeOfType<SendBatchAfterHookContext>();

        ((SendBatchAfterHookContext) capturedAfterContext!).Operation.Should().Be(ProducerOperations.SendBatch);
        capturedAfterContext.ServiceBusNamespaceName.Should().Be(namespaceName);
        capturedAfterContext.EntityPath.Should().Be(queueName);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Targeted_Hooks_Should_Execute()
    {
        const string namespaceName = "test-namespace";
        const string queueName = "test-queue";

        var provider = new InMemoryServiceBusProvider();
        var ns = provider.AddNamespace(namespaceName);
        ns.AddQueue(queueName);

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var sender = client.CreateSender(queueName);

        ProducerBeforeHookContext? capturedBeforeContext = null;
        ProducerAfterHookContext? capturedAfterContext = null;

        provider.AddHook(builder => builder.ForProducer().Before(ctx =>
        {
            capturedBeforeContext = ctx;
            return Task.CompletedTask;
        }, ProducerOperations.SendBatch));

        provider.AddHook(builder => builder.ForProducer().After(ctx =>
        {
            capturedAfterContext = ctx;
            return Task.CompletedTask;
        }, ProducerOperations.SendBatch));

        using var batch = await sender.CreateMessageBatchAsync();

        batch.TryAddMessage(new(BinaryData.FromString("Message 1")));

        await sender.SendMessagesAsync(batch);

        capturedBeforeContext.Should().NotBeNull();

        capturedBeforeContext?.Operation.Should().Be(ProducerOperations.SendBatch);
        capturedBeforeContext?.ServiceBusNamespaceName.Should().Be(namespaceName);
        capturedBeforeContext?.EntityPath.Should().Be(queueName);

        capturedAfterContext.Should().NotBeNull();
        capturedAfterContext?.Operation.Should().Be(ProducerOperations.SendBatch);
        capturedAfterContext?.ServiceBusNamespaceName.Should().Be(namespaceName);
        capturedAfterContext?.EntityPath.Should().Be(queueName);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Hooks_With_Different_Target_Should_Not_Execute()
    {
        const string namespaceName = "test-namespace";
        const string queueName = "test-queue";

        var provider = new InMemoryServiceBusProvider();
        var ns = provider.AddNamespace(namespaceName);
        ns.AddQueue(queueName);

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var sender = client.CreateSender(queueName);

        provider.AddHook(builder => builder.ForConsumer().Before(_ => throw new InvalidOperationException("This hook should not execute"), ConsumerOperations.ReceiveBatch));

        provider.AddHook(builder => builder.ForConsumer().After(_ => throw new InvalidOperationException("This hook should not execute"), ConsumerOperations.ReceiveBatch));

        using var batch = await sender.CreateMessageBatchAsync();

        batch.TryAddMessage(new(BinaryData.FromString("Message 1")));

        await sender.SendMessagesAsync(batch);
    }
}
