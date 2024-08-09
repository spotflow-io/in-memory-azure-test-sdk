using Azure.Messaging.ServiceBus;

using FluentAssertions.Execution;

using Spotflow.InMemory.Azure.ServiceBus;
using Spotflow.InMemory.Azure.ServiceBus.FluentAssertions;

namespace Tests.ServiceBus;

[TestClass]
public class FluentAssertionsTests
{
    [TestMethod]
    public async Task New_Queue_Should_Be_Empty()
    {
        var provider = new InMemoryServiceBusProvider();

        var queue = provider.AddNamespace().AddQueue("test-queue");

        await queue.Should().BeEmptyAsync();
    }

    [TestMethod]
    public async Task New_Subscription_Should_Be_Empty()
    {
        var provider = new InMemoryServiceBusProvider();

        var subscription = provider.AddNamespace().AddTopic("test-queue").AddSubscription("test");

        await subscription.Should().BeEmptyAsync();

    }

    [TestMethod]
    public async Task Non_Empty_Queue_Should_Not_Be_Empty_And_Then_Become_Empty()
    {
        var provider = new InMemoryServiceBusProvider();

        var queue = provider.AddNamespace().AddQueue("test-queue");

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);
        await using var producerClient = client.CreateSender("test-queue");
        await using var consumerClient = client.CreateReceiver("test-queue");

        await producerClient.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")));

        try
        {
            await queue.Should().BeEmptyAsync(maxWaitTime: TimeSpan.FromMilliseconds(100));

            throw new AssertionFailedException("Should have thrown an exception");
        }
        catch (AssertFailedException ex)
        {
            ex.Message.Should().Be("Entity \"test-queue\" should be empty but 1L messages found after 0.1 seconds.");
        }

        var message = await consumerClient.ReceiveMessageAsync();
        await consumerClient.CompleteMessageAsync(message);

        await queue.Should().BeEmptyAsync(maxWaitTime: TimeSpan.FromMilliseconds(100));
    }

    [TestMethod]
    public async Task Non_Empty_Subscription_Should_Not_Be_Empty_And_Then_Become_Empty()
    {
        var provider = new InMemoryServiceBusProvider();

        var topic = provider.AddNamespace().AddTopic("test-topic");
        var subscription = topic.AddSubscription("test");

        await using var client = InMemoryServiceBusClient.FromNamespace(topic.Namespace);
        await using var producerClient = client.CreateSender("test-topic");
        await using var consumerClient = client.CreateReceiver("test-topic", "test");

        await producerClient.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")));

        try
        {
            await subscription.Should().BeEmptyAsync(maxWaitTime: TimeSpan.FromMilliseconds(100));

            throw new AssertionFailedException("Should have thrown an exception");
        }
        catch (AssertFailedException ex)
        {
            ex.Message.Should().Be("Entity \"test-topic/test\" should be empty but 1L messages found after 0.1 seconds.");
        }

        var message = await consumerClient.ReceiveMessageAsync();
        await consumerClient.CompleteMessageAsync(message);

        await subscription.Should().BeEmptyAsync(maxWaitTime: TimeSpan.FromMilliseconds(100));
    }

}