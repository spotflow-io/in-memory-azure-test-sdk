using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus;

namespace Tests.ServiceBus;

[TestClass]
public class ServiceBusSubscriptionFilterTests
{
    private static ServiceBusMessage CreateMessage(string body, string commandType, string? sessionId = null)
    {
        var message = new ServiceBusMessage(BinaryData.FromString(body));
        message.ApplicationProperties["CommandType"] = commandType;

        if (sessionId is not null)
        {
            message.SessionId = sessionId;
        }

        return message;
    }

    private static Func<ServiceBusMessage, bool> CommandTypeFilter(string commandType)
    {
        return message => message.ApplicationProperties.TryGetValue("CommandType", out var value) && value is string s && s == commandType;
    }

    [TestMethod]
    public async Task Single_Messages_Should_Be_Routed_Only_To_Matching_Subscriptions()
    {
        var topic = new InMemoryServiceBusProvider().AddNamespace().AddTopic("test-topic");

        var subscriptionA = topic.AddSubscription("sub-a", new() { Filter = CommandTypeFilter("type-a") });
        var subscriptionB = topic.AddSubscription("sub-b", new() { Filter = CommandTypeFilter("type-b") });
        var subscriptionAll = topic.AddSubscription("sub-all");

        await using var client = InMemoryServiceBusClient.FromNamespace(topic.Namespace);
        await using var sender = client.CreateSender("test-topic");

        await sender.SendMessageAsync(CreateMessage("a-1", "type-a"));
        await sender.SendMessageAsync(CreateMessage("b-1", "type-b"));
        await sender.SendMessageAsync(CreateMessage("a-2", "type-a"));
        await sender.SendMessageAsync(CreateMessage("c-1", "type-c"));

        subscriptionA.ActiveMessageCount.Should().Be(2);
        subscriptionB.ActiveMessageCount.Should().Be(1);
        subscriptionAll.ActiveMessageCount.Should().Be(4);

        await using var receiverA = client.CreateReceiver("test-topic", "sub-a");
        await using var receiverB = client.CreateReceiver("test-topic", "sub-b");

        var messagesA = await receiverA.ReceiveMessagesAsync(10, TimeSpan.FromMilliseconds(100));
        var messagesB = await receiverB.ReceiveMessagesAsync(10, TimeSpan.FromMilliseconds(100));

        messagesA.Select(m => m.Body.ToString()).Should().Equal("a-1", "a-2");
        messagesB.Select(m => m.Body.ToString()).Should().Equal("b-1");
    }

    [TestMethod]
    public async Task Batch_Messages_Should_Be_Routed_Only_To_Matching_Subscriptions()
    {
        var topic = new InMemoryServiceBusProvider().AddNamespace().AddTopic("test-topic");

        var subscriptionA = topic.AddSubscription("sub-a", new() { Filter = CommandTypeFilter("type-a") });
        var subscriptionB = topic.AddSubscription("sub-b", new() { Filter = CommandTypeFilter("type-b") });
        var subscriptionNone = topic.AddSubscription("sub-none", new() { Filter = _ => false });

        await using var client = InMemoryServiceBusClient.FromNamespace(topic.Namespace);
        await using var sender = client.CreateSender("test-topic");

        using var batch = await sender.CreateMessageBatchAsync();

        batch.TryAddMessage(CreateMessage("a-1", "type-a")).Should().BeTrue();
        batch.TryAddMessage(CreateMessage("b-1", "type-b")).Should().BeTrue();
        batch.TryAddMessage(CreateMessage("a-2", "type-a")).Should().BeTrue();

        await sender.SendMessagesAsync(batch);

        await sender.SendMessagesAsync([CreateMessage("b-2", "type-b"), CreateMessage("a-3", "type-a")]);

        subscriptionA.ActiveMessageCount.Should().Be(3);
        subscriptionB.ActiveMessageCount.Should().Be(2);
        subscriptionNone.ActiveMessageCount.Should().Be(0);

        await using var receiverA = client.CreateReceiver("test-topic", "sub-a");

        var messagesA = await receiverA.ReceiveMessagesAsync(10, TimeSpan.FromMilliseconds(100));

        messagesA.Select(m => m.Body.ToString()).Should().Equal("a-1", "a-2", "a-3");
    }

    [TestMethod]
    public async Task Session_Subscriptions_Should_Respect_Filter()
    {
        var topic = new InMemoryServiceBusProvider().AddNamespace().AddTopic("test-topic");

        var subscriptionA = topic.AddSubscription("sub-a", new() { EnableSessions = true, Filter = CommandTypeFilter("type-a") });
        var subscriptionB = topic.AddSubscription("sub-b", new() { EnableSessions = true, Filter = CommandTypeFilter("type-b") });

        await using var client = InMemoryServiceBusClient.FromNamespace(topic.Namespace);
        await using var sender = client.CreateSender("test-topic");

        await sender.SendMessageAsync(CreateMessage("a-1", "type-a", sessionId: "session-1"));
        await sender.SendMessageAsync(CreateMessage("b-1", "type-b", sessionId: "session-1"));

        subscriptionA.ActiveMessageCount.Should().Be(1);
        subscriptionB.ActiveMessageCount.Should().Be(1);

        await using var sessionReceiverA = await client.AcceptSessionAsync("test-topic", "sub-a", "session-1");

        var message = await sessionReceiverA.ReceiveMessageAsync(TimeSpan.FromMilliseconds(100));

        message.Should().NotBeNull();
        message!.Body.ToString().Should().Be("a-1");

        var nextMessage = await sessionReceiverA.ReceiveMessageAsync(TimeSpan.FromMilliseconds(100));

        nextMessage.Should().BeNull();
    }

    [TestMethod]
    public void Filter_Should_Be_Exposed_On_Subscription()
    {
        var topic = new InMemoryServiceBusProvider().AddNamespace().AddTopic("test-topic");

        var filter = CommandTypeFilter("type-a");

        topic.AddSubscription("with-filter", new() { Filter = filter }).Filter.Should().BeSameAs(filter);
        topic.AddSubscription("without-filter").Filter.Should().BeNull();
    }
}
