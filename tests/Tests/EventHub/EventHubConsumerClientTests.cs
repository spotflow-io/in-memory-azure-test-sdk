using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

using Spotflow.InMemory.Azure.EventHubs;

namespace Tests.EventHub;

[TestClass]
public class EventHubConsumerClientTests
{
    [TestMethod]
    public async Task Constructor_With_Connection_String_Should_Succeed()
    {
        var provider = new InMemoryEventHubProvider();

        var eventHub = provider.AddNamespace().AddEventHub("test", 2);

        var connectionString = eventHub.GetConnectionString();

        await using var client = new InMemoryEventHubConsumerClient("cg", connectionString, provider);

        client.EventHubName.Should().Be("test");
        client.FullyQualifiedNamespace.Should().Be(eventHub.Namespace.FullyQualifiedNamespace);
        client.Identifier.Should().NotBeNullOrWhiteSpace();
        client.IsClosed.Should().BeFalse();
        client.ConsumerGroup.Should().Be("cg");
    }

    [TestMethod]
    public async Task GetProperties_Should_Return_Correct_Info()
    {
        var provider = new InMemoryEventHubProvider();

        var eventHub = provider.AddNamespace().AddEventHub("test", 2);

        await using var client = InMemoryEventHubConsumerClient.FromEventHub(eventHub);

        var properties = await client.GetEventHubPropertiesAsync();

        properties.PartitionIds.Should().BeEquivalentTo(["0", "1"]);
        properties.Name.Should().Be("test");
        properties.CreatedOn.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromHours(2));
    }


    [TestMethod]
    public async Task GetPartitionProperties_Should_Return_Correct_Offset()
    {
        var provider = new InMemoryEventHubProvider();

        var eventHub = provider.AddNamespace().AddEventHub("test", 2);

        await using var consumerClient = InMemoryEventHubConsumerClient.FromEventHub(eventHub);
        await using var producerClient = InMemoryEventHubProducerClient.FromEventHub(eventHub);

        var propertiesBeforeSend = await consumerClient.GetPartitionPropertiesAsync("0");

        propertiesBeforeSend.LastEnqueuedOffsetString.Should().Be("-1");

        await producerClient.SendAsync([new EventData()], new SendEventOptions { PartitionId = "0" });

        var propertiesAfterSend1 = await consumerClient.GetPartitionPropertiesAsync("0");
        propertiesAfterSend1.LastEnqueuedOffsetString.Should().Be("0");

        await producerClient.SendAsync([new EventData()], new SendEventOptions { PartitionId = "0" });

        var propertiesAfterSend2 = await consumerClient.GetPartitionPropertiesAsync("0");
        propertiesAfterSend2.LastEnqueuedOffsetString.Should().Be("26");

    }
}
