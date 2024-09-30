using Azure.Messaging.ServiceBus;

using FluentAssertions.Execution;

using Microsoft.Extensions.Time.Testing;

using Spotflow.InMemory.Azure.ServiceBus;

namespace Tests.ServiceBus;


[TestClass]
public class ServiceBusReceiverTests
{
    [TestMethod]
    public async Task Constructor_For_Queue_Should_Succeed()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);

        await using var receiver = new InMemoryServiceBusReceiver(client, "test-queue", options: new() { PrefetchCount = 42 });

        receiver.ReceiveMode.Should().Be(ServiceBusReceiveMode.PeekLock);
        receiver.FullyQualifiedNamespace.Should().Be(ns.FullyQualifiedNamespace);
        receiver.EntityPath.Should().Be("test-queue");
        receiver.Identifier.Should().NotBeNullOrWhiteSpace();
        receiver.IsClosed.Should().BeFalse();
        receiver.PrefetchCount.Should().Be(42);
    }


    [TestMethod]
    public async Task Constructor_For_Topic_Subscription_Should_Succeed()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);

        await using var receiver = new InMemoryServiceBusReceiver(client, "test-topic", "test-subscription", options: new() { PrefetchCount = 42 });

        receiver.ReceiveMode.Should().Be(ServiceBusReceiveMode.PeekLock);
        receiver.FullyQualifiedNamespace.Should().Be(ns.FullyQualifiedNamespace);
        receiver.EntityPath.Should().Be("test-topic/subscriptions/test-subscription");
        receiver.Identifier.Should().NotBeNullOrWhiteSpace();
        receiver.IsClosed.Should().BeFalse();
        receiver.PrefetchCount.Should().Be(42);
    }

    [TestMethod]
    public async Task Queue_Receiver_Should_Receive_Message_Sent_After_Receive_Operation_Started()
    {
        var provider = new InMemoryServiceBusProvider();

        var queue = provider.AddNamespace().AddQueue("test-queue");

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);

        await using var sender = client.CreateSender("test-queue");
        await using var receiver = client.CreateReceiver("test-queue");

        var receivedMessageTask = receiver.ReceiveMessageAsync();

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")));

        var receivedMessage = await receivedMessageTask;

        receivedMessage.Body.ToString().Should().Be("Hello, world!");
    }

    [TestMethod]
    public async Task Topic_Receivers_Should_Receive_Message_Sent_After_Receive_Operation_Started()
    {
        var provider = new InMemoryServiceBusProvider();

        var topic = provider.AddNamespace().AddTopic("test-topic");

        topic.AddSubscription("subscription-1");
        topic.AddSubscription("subscription-2");

        await using var client = InMemoryServiceBusClient.FromNamespace(topic.Namespace);

        await using var sender = client.CreateSender("test-topic");
        await using var receiver1 = client.CreateReceiver("test-topic", "subscription-1");
        await using var receiver2 = client.CreateReceiver("test-topic", "subscription-2");

        var receivedMessageTask1 = receiver1.ReceiveMessageAsync();
        var receivedMessageTask2 = receiver2.ReceiveMessageAsync();

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")));

        var receivedMessage1 = await receivedMessageTask1;
        var receivedMessage2 = await receivedMessageTask2;

        receivedMessage1.Body.ToString().Should().Be("Hello, world!");
        receivedMessage2.Body.ToString().Should().Be("Hello, world!");

    }

    [TestMethod]
    public async Task Abandoned_Message_Should_Be_Recieved_Again()
    {
        var provider = new InMemoryServiceBusProvider();

        var queue = provider.AddNamespace().AddQueue("test-queue");

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);

        await using var sender = client.CreateSender("test-queue");
        await using var receiver = client.CreateReceiver("test-queue");

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")));

        var receivedMessage = await receiver.ReceiveMessageAsync();

        var additionalReceivedMessagesBeforeAbandon = await receiver.ReceiveMessagesAsync(1, TimeSpan.FromMilliseconds(100));

        additionalReceivedMessagesBeforeAbandon.Should().BeEmpty();

        await receiver.AbandonMessageAsync(receivedMessage);

        var additionalReceivedMessagesAfterAbandon = await receiver.ReceiveMessagesAsync(1, TimeSpan.FromMinutes(1));

        additionalReceivedMessagesAfterAbandon.Should().HaveCount(1);
        additionalReceivedMessagesAfterAbandon[0].Body.ToString().Should().Be("Hello, world!");
    }

    [TestMethod]
    public async Task Completed_Message_Should_Not_Be_Recieved_Again()
    {
        var timeProvider = new FakeTimeProvider();

        var provider = new InMemoryServiceBusProvider(timeProvider);

        var queue = provider.AddNamespace().AddQueue("test-queue");

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);

        await using var sender = client.CreateSender("test-queue");
        await using var receiver = client.CreateReceiver("test-queue");

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")));

        var message = await receiver.ReceiveMessageAsync();

        await receiver.CompleteMessageAsync(message);

        timeProvider.Advance(TimeSpan.FromHours(1));

        var messagesAfterComplete = await receiver.ReceiveMessagesAsync(1, TimeSpan.FromMilliseconds(100));

        messagesAfterComplete.Should().BeEmpty();

    }

    [TestMethod]
    public async Task Expired_Message_Cannot_Be_Completed()
    {
        var timeProvider = new FakeTimeProvider();

        var provider = new InMemoryServiceBusProvider(timeProvider);

        var queue = provider.AddNamespace().AddQueue("test-queue", new() { LockTime = TimeSpan.FromMinutes(2) });

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);

        await using var sender = client.CreateSender("test-queue");
        await using var receiver = client.CreateReceiver("test-queue");

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")));

        var message = await receiver.ReceiveMessageAsync();

        timeProvider.Advance(TimeSpan.FromMinutes(3));

        var act = () => receiver.CompleteMessageAsync(message);

        await act.Should()
            .ThrowAsync<ServiceBusException>()
            .Where(ex => ex.Reason == ServiceBusFailureReason.MessageLockLost);
    }


    [TestMethod]
    public async Task Message_Count_On_Queue_Should_Be_Reported()
    {
        var timeProvider = new FakeTimeProvider();

        var provider = new InMemoryServiceBusProvider(timeProvider);

        var queue = provider.AddNamespace().AddQueue("test-queue", new() { LockTime = TimeSpan.FromMinutes(2) });

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);

        await using var sender = client.CreateSender("test-queue");
        await using var receiver = client.CreateReceiver("test-queue");

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")));
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")));

        queue.ActiveMessageCount.Should().Be(2);
        queue.MessageCount.Should().Be(2);

        var message = await receiver.ReceiveMessageAsync();

        queue.ActiveMessageCount.Should().Be(1);
        queue.MessageCount.Should().Be(2);

        await receiver.CompleteMessageAsync(message);

        queue.ActiveMessageCount.Should().Be(1);
        queue.MessageCount.Should().Be(1);

        _ = await receiver.ReceiveMessageAsync();

        queue.ActiveMessageCount.Should().Be(0);
        queue.MessageCount.Should().Be(1);

        timeProvider.Advance(TimeSpan.FromMinutes(3));

        queue.ActiveMessageCount.Should().Be(1);
        queue.MessageCount.Should().Be(1);
    }

    [TestMethod]
    public async Task Message_Count_On_Topic_Subscription_Should_Be_Reported()
    {
        var timeProvider = new FakeTimeProvider();

        var provider = new InMemoryServiceBusProvider(timeProvider);

        var topic = provider.AddNamespace().AddTopic("test-topic");

        var subscription = topic.AddSubscription("test-subscription", new() { LockTime = TimeSpan.FromMinutes(2) });

        await using var client = InMemoryServiceBusClient.FromNamespace(topic.Namespace);

        await using var sender = client.CreateSender("test-topic");
        await using var receiver = client.CreateReceiver("test-topic", "test-subscription");

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")));
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")));

        subscription.ActiveMessageCount.Should().Be(2);
        subscription.MessageCount.Should().Be(2);

        var message = await receiver.ReceiveMessageAsync();

        subscription.ActiveMessageCount.Should().Be(1);
        subscription.MessageCount.Should().Be(2);

        await receiver.CompleteMessageAsync(message);

        subscription.ActiveMessageCount.Should().Be(1);
        subscription.MessageCount.Should().Be(1);

        _ = await receiver.ReceiveMessageAsync();

        subscription.ActiveMessageCount.Should().Be(0);
        subscription.MessageCount.Should().Be(1);

        timeProvider.Advance(TimeSpan.FromMinutes(3));

        subscription.ActiveMessageCount.Should().Be(1);
        subscription.MessageCount.Should().Be(1);
    }


    [TestMethod]
    public async Task Received_Message_Should_Have_All_Expected_Properties()
    {
        var timeProvider = new FakeTimeProvider();

        var provider = new InMemoryServiceBusProvider(timeProvider);

        var queue = provider.AddNamespace().AddQueue("test-queue");

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);

        await using var sender = client.CreateSender("test-queue");
        await using var receiver = client.CreateReceiver("test-queue");

        var payload = BinaryData.FromString("Test payload.");

        var message = new ServiceBusMessage(payload)
        {
            ApplicationProperties = { { "test-app-property", "test-app-property-value" } },
            Subject = "test-subject",
            ContentType = "test-content-type",
            CorrelationId = "test-correlation-id",
            MessageId = "test-message-id",
            PartitionKey = "test-partition-key",
            ReplyTo = "test-reply-to",
            ReplyToSessionId = "test-reply-to-session-id"
        };

        await sender.SendMessagesAsync([message]);

        var receivedMessage = await receiver.ReceiveMessageAsync();

        using var assertionScope = new AssertionScope();

        receivedMessage.ApplicationProperties.Count.Should().Be(1);
        receivedMessage.ApplicationProperties["test-app-property"].Should().Be("test-app-property-value");

        //receivedMessage.Subject.Should().Be("test-subject");
        receivedMessage.ContentType.Should().Be("test-content-type");
        receivedMessage.CorrelationId.Should().Be("test-correlation-id");
        receivedMessage.MessageId.Should().Be("test-message-id");
        //receivedMessage.PartitionKey.Should().Be("test-partition-key");
        //receivedMessage.ReplyTo.Should().Be("test-reply-to");
        receivedMessage.ReplyToSessionId.Should().Be("test-reply-to-session-id");

        receivedMessage.EnqueuedTime.Should().Be(timeProvider.GetUtcNow());
        receivedMessage.SequenceNumber.Should().Be(0);
        receivedMessage.SessionId.Should().BeNull();

    }

}
