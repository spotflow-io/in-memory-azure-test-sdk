using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;

using Spotflow.InMemory.Azure.EventHubs;

namespace Tests.EventHub;

[TestClass]
public class EventHubProducerClientTests
{
    [TestMethod]
    public async Task Constructor_With_Connection_String_Should_Succeed()
    {
        var provider = new InMemoryEventHubProvider();

        var eventHub = provider.AddNamespace().AddEventHub("test", 2);

        var connectionString = eventHub.CreateConnectionString();

        await using var client = new InMemoryEventHubProducerClient(connectionString, provider);

        client.EventHubName.Should().Be("test");
        client.FullyQualifiedNamespace.Should().Be(eventHub.Namespace.FullyQualifiedNamespace);
        client.Identifier.Should().NotBeNullOrWhiteSpace();
        client.IsClosed.Should().BeFalse();
    }


    [TestMethod]
    public async Task SystemProperties_ShouldBeSent()
    {
        var eventHub = new InMemoryEventHubProvider().AddNamespace().AddEventHub("test-eh", 1);

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);
        await using var consumer = InMemoryPartitionReceiver.FromEventHub("$default", "0", EventPosition.Earliest, eventHub);

        var sentEventData = new EventData { MessageId = "test-mi", ContentType = "test-ct", CorrelationId = "test-ci" };

        await producer.SendAsync([sentEventData], new SendEventOptions { PartitionKey = "test-pk" });

        var batch = await consumer.ReceiveBatchAsync(100, TimeSpan.Zero);

        var eventData = batch.Should().ContainSingle().Which;

        eventData.MessageId.Should().Be("test-mi");
        eventData.ContentType.Should().Be("test-ct");
        eventData.CorrelationId.Should().Be("test-ci");
        eventData.PartitionKey.Should().Be("test-pk");

    }

}
