using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus;

using Tests.Utils;

namespace Tests.ServiceBus;

[TestClass]
public class ServiceBusSenderTests
{
    [TestMethod]
    public async Task Constructor_Should_Succeed()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);

        await using var sender = new InMemoryServiceBusSender(client, "test-queue");

        sender.FullyQualifiedNamespace.Should().Be(ns.FullyQualifiedNamespace);
        sender.EntityPath.Should().Be("test-queue");
        sender.Identifier.Should().NotBeNullOrWhiteSpace();
        sender.IsClosed.Should().BeFalse();
    }

    [TestMethod]
    public async Task Send_Batch_Should_Succeed()
    {
        var provider = new InMemoryServiceBusProvider();
        var ns = provider.AddNamespace();
        var queue = ns.AddQueue("test-queue");

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);

        await using var sender = client.CreateSender("test-queue");

        using var batch = await sender.CreateMessageBatchAsync();

        batch.TryAddMessage(new ServiceBusMessage(BinaryData.FromString("Message 1")));
        batch.TryAddMessage(new ServiceBusMessage(BinaryData.FromString("Message 2")));
        batch.TryAddMessage(new ServiceBusMessage(BinaryData.FromString("Message 3")));

        await sender.SendMessagesAsync(batch);

        await using var receiver = client.CreateReceiver("test-queue");

        var messages = await receiver.ReceiveMessagesAsync(3, TimeSpan.FromMilliseconds(100));

        messages.Select(m => m.Body.ToString()).Should().BeEquivalentTo(["Message 1", "Message 2", "Message 3"]);
        messages.Select(m => m.SequenceNumber).Should().BeEquivalentTo([0, 1, 2]);

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Missing_Namespace_Should_Throw()
    {
        await using var sender = await ImplementationProvider.GetServiceBusSenderAsync(missingNamespace: true);

        var act = () => sender.SendMessageAsync(new ServiceBusMessage());

        await act.Should()
            .ThrowAsync<ServiceBusException>()
            .Where(ex => ex.Reason == ServiceBusFailureReason.ServiceCommunicationProblem)
            .Where(ex => ex.IsTransient == true)
            .Where(ex => ex.Message.Contains("ErrorCode: HostNotFound"));
    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    [DataRow(true, DisplayName = "Topic")]
    [DataRow(false, DisplayName = "Queue")]
    public async Task Send_To_Missing_Entity_Should_Fail(bool useTopics)
    {
        await using var sender = await ImplementationProvider.GetServiceBusSenderAsync(missingEntity: true, useTopics: useTopics);

        var act = () => sender.SendMessageAsync(new ServiceBusMessage());

        await act.Should()
            .ThrowAsync<ServiceBusException>()
            .Where(ex => ex.Reason == ServiceBusFailureReason.MessagingEntityNotFound)
            .Where(ex => ex.IsTransient == false);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Send_To_Session_Enabled_Queue_Without_Session_Id_Should_Fail()
    {
        await using var sender = await ImplementationProvider.GetServiceBusSenderAsync(withSessions: true);

        var message = new ServiceBusMessage();

        var act = () => sender.SendMessageAsync(message);

        var expectedMessagePrefix = "" +
            "The SessionId was not set on a message, " +
            "and it cannot be sent to the entity. " +
            "Entities that have session support enabled can " +
            "only receive messages that have the SessionId set to a valid value.";

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .Where(ex => ex.Message.StartsWith(expectedMessagePrefix));
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Send_To_Topic_Without_Session_Id_Should_Not_Throw_Even_If_There_Are_Sessionless_Subscriptions()
    {
        await using var sender = await ImplementationProvider.GetServiceBusSenderAsync(withSessions: true, useTopics: true);

        var message = new ServiceBusMessage();

        var act = () => sender.SendMessageAsync(message);

        await act.Should().NotThrowAsync();
    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Send_To_Session_Disabled_Queue_With_Session_Id_Should_Succeed()
    {
        await using var sender = await ImplementationProvider.GetServiceBusSenderAsync();

        var message = new ServiceBusMessage { SessionId = "test" };

        var act = () => sender.SendMessageAsync(message);

        await act.Should().NotThrowAsync();
    }

}
