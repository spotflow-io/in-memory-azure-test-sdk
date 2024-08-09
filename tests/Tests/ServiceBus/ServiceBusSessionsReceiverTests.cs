using Azure.Messaging.ServiceBus;

using Microsoft.Extensions.Time.Testing;

using Spotflow.InMemory.Azure.ServiceBus;

namespace Tests.ServiceBus;

[TestClass]
public class ServiceBusSessionsReceiverTests
{

    [TestMethod]
    public async Task Existing_Sessions_In_Queue_Should_Be_Directly_Received()
    {
        var provider = new InMemoryServiceBusProvider();

        var queue = provider.AddNamespace().AddQueue("test-queue", new() { EnableSessions = true });

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);

        await using var sender = client.CreateSender("test-queue");

        var message1 = new ServiceBusMessage(BinaryData.FromString("Message 1")) { SessionId = "session-1" };
        var message2 = new ServiceBusMessage(BinaryData.FromString("Message 2")) { SessionId = "session-2" };

        await sender.SendMessagesAsync([message1, message2]);

        await using var sessionReceiver1 = await client.AcceptSessionAsync("test-queue", "session-1");
        await using var sessionReceiver2 = await client.AcceptSessionAsync("test-queue", "session-2");

        sessionReceiver1.SessionId.Should().Be("session-1");
        sessionReceiver1.EntityPath.Should().Be("test-queue");
        sessionReceiver1.Identifier.Should().NotBeNullOrWhiteSpace();
        sessionReceiver2.SessionId.Should().Be("session-2");
        sessionReceiver2.EntityPath.Should().Be("test-queue");
        sessionReceiver2.Identifier.Should().NotBeNullOrWhiteSpace();

        var receivedMessage1 = await sessionReceiver1.ReceiveMessageAsync();
        var receivedMessage2 = await sessionReceiver2.ReceiveMessageAsync();

        receivedMessage1.Body.ToString().Should().Be("Message 1");
        receivedMessage2.Body.ToString().Should().Be("Message 2");
    }

    [TestMethod]
    public async Task Existing_Sessions_In_Topic_Should_Be_Directly_Received()
    {
        var provider = new InMemoryServiceBusProvider();

        var topic = provider.AddNamespace().AddTopic("test-topic");

        topic.AddSubscription("sub", new() { EnableSessions = true });


        await using var client = InMemoryServiceBusClient.FromNamespace(topic.Namespace);

        await using var sender = client.CreateSender("test-topic");

        var message1 = new ServiceBusMessage(BinaryData.FromString("Message 1")) { SessionId = "session-1" };
        var message2 = new ServiceBusMessage(BinaryData.FromString("Message 2")) { SessionId = "session-2" };

        await sender.SendMessagesAsync([message1, message2]);

        await using var sessionReceiver1 = await client.AcceptSessionAsync("test-topic", "sub", "session-1");
        await using var sessionReceiver2 = await client.AcceptSessionAsync("test-topic", "sub", "session-2");

        sessionReceiver1.SessionId.Should().Be("session-1");
        sessionReceiver1.EntityPath.Should().Be("test-topic/subscriptions/sub");
        sessionReceiver1.Identifier.Should().NotBeNullOrWhiteSpace();
        sessionReceiver2.SessionId.Should().Be("session-2");
        sessionReceiver2.EntityPath.Should().Be("test-topic/subscriptions/sub");
        sessionReceiver2.Identifier.Should().NotBeNullOrWhiteSpace();

        var receivedMessage1 = await sessionReceiver1.ReceiveMessageAsync();
        var receivedMessage2 = await sessionReceiver2.ReceiveMessageAsync();

        receivedMessage1.Body.ToString().Should().Be("Message 1");
        receivedMessage2.Body.ToString().Should().Be("Message 2");
    }

    [TestMethod]
    public async Task Empty_Session_Is_Not_Available()
    {
        var timeProvider = new FakeTimeProvider();

        var provider = new InMemoryServiceBusProvider(timeProvider);

        var queue = provider.AddNamespace().AddQueue("test-queue", new() { EnableSessions = true });

        var clientOptions = new ServiceBusClientOptions { RetryOptions = new() { MaxDelay = TimeSpan.FromMinutes(3) } };

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace, clientOptions);

        await using var sender = client.CreateSender("test-queue");

        await sender.SendMessageAsync(new(BinaryData.FromString("Test Message")) { SessionId = "session-1" });

        await using (var sessionReceiver = await client.AcceptSessionAsync("test-queue", "session-1"))
        {
            var receivedMessage = await sessionReceiver.ReceiveMessageAsync();

            receivedMessage.Body.ToString().Should().Be("Test Message");

            await sessionReceiver.CompleteMessageAsync(receivedMessage);
        }

        var withSessionIdAct = () => client.AcceptSessionAsync("test-queue", "session-1");

        var withoutSessionIdAct = async () =>
        {
            var task = client.AcceptNextSessionAsync("test-queue");

            while (!task.IsCompleted)
            {
                timeProvider.Advance(TimeSpan.FromMinutes(1));
                await Task.Delay(100);
            }

            await task;
        };

        await withSessionIdAct.Should()
            .ThrowAsync<ServiceBusException>()
            .Where(e => e.Reason == ServiceBusFailureReason.GeneralError);

        await withoutSessionIdAct.Should()
            .ThrowAsync<ServiceBusException>()
            .Where(e => e.Reason == ServiceBusFailureReason.ServiceTimeout);
    }

    [TestMethod]
    public async Task Session_Should_BeRelease_And_Reacquired()
    {
        var timeProvider = new FakeTimeProvider();

        var provider = new InMemoryServiceBusProvider(timeProvider);

        var queue = provider.AddNamespace().AddQueue("test-queue", new() { EnableSessions = true });

        var clientOptions = new ServiceBusClientOptions { RetryOptions = new() { MaxDelay = TimeSpan.FromMinutes(3) } };

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace, clientOptions);

        await using var sender = client.CreateSender("test-queue");

        var message1 = new ServiceBusMessage(BinaryData.FromString("Test Message 1")) { SessionId = "session-1" };
        var message2 = new ServiceBusMessage(BinaryData.FromString("Test Message 2")) { SessionId = "session-1" };

        await sender.SendMessagesAsync([message1, message2]);

        await using (var sessionReceiver = await client.AcceptNextSessionAsync("test-queue"))
        {
            var receivedMessage = await sessionReceiver.ReceiveMessageAsync();

            receivedMessage.Body.ToString().Should().Be("Test Message 1");

            await sessionReceiver.CompleteMessageAsync(receivedMessage);

            var acceptAgain = async () =>
            {
                var task = client.AcceptNextSessionAsync("test-queue");

                while (!task.IsCompleted)
                {
                    timeProvider.Advance(TimeSpan.FromMinutes(4));
                    await Task.Delay(100);
                }

                await task;
            };

            await acceptAgain.Should()
                .ThrowAsync<ServiceBusException>()
                .Where(e => e.Reason == ServiceBusFailureReason.ServiceTimeout);

        }

        await using (var sessionReceiver = await client.AcceptNextSessionAsync("test-queue"))
        {
            var receivedMessage = await sessionReceiver.ReceiveMessageAsync();

            receivedMessage.Body.ToString().Should().Be("Test Message 2");
        }

    }


    [TestMethod]
    public async Task Lost_Session_Lock_Should_Cause_All_Operations_To_Fail()
    {
        var timeProvider = new FakeTimeProvider();

        var provider = new InMemoryServiceBusProvider(timeProvider);

        var queue = provider.AddNamespace().AddQueue("test-queue", new() { EnableSessions = true, LockTime = TimeSpan.FromMinutes(2) });

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);

        await using var sender = client.CreateSender("test-queue");

        var message = new ServiceBusMessage(BinaryData.FromString("Test Message")) { SessionId = "session-1" };

        await sender.SendMessageAsync(message);

        await using var sessionReceiver = await client.AcceptNextSessionAsync("test-queue");

        var receivedMessage = await sessionReceiver.ReceiveMessageAsync();

        receivedMessage.Body.ToString().Should().Be("Test Message");

        timeProvider.Advance(TimeSpan.FromMinutes(3));

        var receiveAct = () => sessionReceiver.ReceiveMessageAsync();
        var completeAct = () => sessionReceiver.CompleteMessageAsync(receivedMessage);
        var renewAct = () => sessionReceiver.RenewSessionLockAsync();
        var abandonAct = () => sessionReceiver.AbandonMessageAsync(receivedMessage);

        await receiveAct.Should()
            .ThrowAsync<ServiceBusException>()
            .Where(e => e.Reason == ServiceBusFailureReason.SessionLockLost);

        await completeAct.Should()
            .ThrowAsync<ServiceBusException>()
            .Where(e => e.Reason == ServiceBusFailureReason.SessionLockLost);

        await renewAct.Should()
            .ThrowAsync<ServiceBusException>()
            .Where(e => e.Reason == ServiceBusFailureReason.SessionLockLost);

        await abandonAct.Should()
            .ThrowAsync<ServiceBusException>()
            .Where(e => e.Reason == ServiceBusFailureReason.SessionLockLost);

    }

    [TestMethod]
    public async Task Expired_Message_Cannot_Be_Completed()
    {
        var timeProvider = new FakeTimeProvider();

        var provider = new InMemoryServiceBusProvider(timeProvider);

        var queue = provider.AddNamespace().AddQueue("test-queue", new() { EnableSessions = true, LockTime = TimeSpan.FromMinutes(2) });

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);

        await using var sender = client.CreateSender("test-queue");

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")) { SessionId = "session-1" });

        await using var receiver = await client.AcceptNextSessionAsync("test-queue");

        var message = await receiver.ReceiveMessageAsync();

        timeProvider.Advance(TimeSpan.FromMinutes(1));

        await receiver.RenewSessionLockAsync();

        timeProvider.Advance(TimeSpan.FromMinutes(1.5));

        var act = () => receiver.CompleteMessageAsync(message);

        await act.Should()
            .ThrowAsync<ServiceBusException>()
            .Where(ex => ex.Reason == ServiceBusFailureReason.MessageLockLost);
    }

    [TestMethod]
    public async Task Session_State_Can_Be_Set_And_Get()
    {
        var provider = new InMemoryServiceBusProvider();

        var queue = provider.AddNamespace().AddQueue("test-queue", new() { EnableSessions = true, LockTime = TimeSpan.FromMinutes(2) });

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace);

        await using var sender = client.CreateSender("test-queue");

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")) { SessionId = "session-1" });

        await using var receiver = await client.AcceptNextSessionAsync("test-queue");

        await receiver.SetSessionStateAsync(BinaryData.FromString("Session State"));

        var sessionState = await receiver.GetSessionStateAsync();

        sessionState.ToString().Should().Be("Session State");

    }

    [TestMethod]
    public async Task Session_Should_Become_Available_When_Message_Lock_Expires()
    {
        var timeProvider = new FakeTimeProvider();

        var provider = new InMemoryServiceBusProvider(timeProvider);

        var queue = provider.AddNamespace().AddQueue("test-queue", new() { EnableSessions = true, LockTime = TimeSpan.FromMinutes(2) });

        await using var client = InMemoryServiceBusClient.FromNamespace(queue.Namespace, new() { RetryOptions = new() { MaxDelay = TimeSpan.FromSeconds(16) } });

        await using var sender = client.CreateSender("test-queue");

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Hello, world!")) { SessionId = "session-1" });

        await using var receiver1 = await client.AcceptNextSessionAsync("test-queue");

        var message1 = await receiver1.ReceiveMessageAsync();

        message1.Body.ToString().Should().Be("Hello, world!");

        var acceptAgain = async () =>
        {
            var task = client.AcceptNextSessionAsync("test-queue");

            while (!task.IsCompleted)
            {
                timeProvider.Advance(TimeSpan.FromSeconds(18));
                await Task.Delay(100);
            }
            await task;
        };

        await acceptAgain.Should()
            .ThrowAsync<ServiceBusException>()
            .Where(e => e.Reason == ServiceBusFailureReason.ServiceTimeout);

        await receiver1.DisposeAsync();

        timeProvider.Advance(TimeSpan.FromMinutes(2));

        await using var receiver2 = await client.AcceptNextSessionAsync("test-queue");

        var message2 = await receiver2.ReceiveMessageAsync();

        message2.Body.ToString().Should().Be("Hello, world!");

    }

}
