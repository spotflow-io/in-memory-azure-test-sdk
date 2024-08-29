using System.Linq;

using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;

using Microsoft.Extensions.Time.Testing;

using Spotflow.InMemory.Azure.EventHubs;
using Spotflow.InMemory.Azure.EventHubs.Resources;

namespace Tests.EventHub;

[TestClass]
public class EventHubTests
{
    [TestMethod]
    public void ConnectionString_ShouldBeReturned()
    {
        var eventHub = new InMemoryEventHubProvider()
            .AddNamespace()
            .AddEventHub("test-eh", 2);

        var connectionString = eventHub.CreateConnectionString();

        var connection = new EventHubConnection(connectionString);

        connection.FullyQualifiedNamespace.Should().Be(eventHub.Namespace.FullyQualifiedNamespace);
        connection.EventHubName.Should().Be(eventHub.Name);
    }

    [TestMethod]

    public async Task Custom_Partition_Initial_State_Should_Be_Used_For_All_Partitions()
    {
        var timeProvider = new FakeTimeProvider();

        timeProvider.SetUtcNow(new DateTimeOffset(2024, 6, 28, 19, 27, 30, TimeSpan.Zero));

        var initialState = new InMemoryPartitionInitialState(42, 43, new DateTimeOffset(2024, 8, 28, 19, 27, 30, TimeSpan.Zero));

        var eventHub = new InMemoryEventHubProvider(timeProvider: timeProvider)
            .AddNamespace()
            .AddEventHub("test-eh", 2, initialState);

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);
        await using var consumer0 = InMemoryPartitionReceiver.FromEventHub("$default", "0", EventPosition.Earliest, eventHub);
        await using var consumer1 = InMemoryPartitionReceiver.FromEventHub("$default", "1", EventPosition.Earliest, eventHub);

        const int eventsCount = 200;

        var content = BinaryData.FromString("abc");

        foreach (var i in Enumerable.Range(0, eventsCount))
        {
            await producer.SendAsync([new EventData(content)], new SendEventOptions { PartitionId = "0" });
            await producer.SendAsync([new EventData(content)], new SendEventOptions { PartitionId = "1" });
        }

        var batch0 = await consumer0.ReceiveBatchAsync(100, TimeSpan.Zero);
        var batch1 = await consumer1.ReceiveBatchAsync(100, TimeSpan.Zero);

        batch0.Should().HaveCount(100);
        batch1.Should().HaveCount(100);

        batch0.ElementAt(0).SequenceNumber.Should().Be(43);
        batch0.ElementAt(0).Offset.Should().Be(43);
        batch0.ElementAt(0).EnqueuedTime.Should().Be(timeProvider.GetUtcNow());
        batch0.ElementAt(1).SequenceNumber.Should().Be(44);
        batch0.ElementAt(1).Offset.Should().Be(43 + content.ToMemory().Length);

        batch1.ElementAt(0).SequenceNumber.Should().Be(43);
        batch1.ElementAt(0).Offset.Should().Be(43);
        batch1.ElementAt(0).EnqueuedTime.Should().Be(timeProvider.GetUtcNow());
        batch1.ElementAt(1).SequenceNumber.Should().Be(44);
        batch1.ElementAt(1).Offset.Should().Be(43 + content.ToMemory().Length);

    }

    [TestMethod]

    public async Task Default_Partition_Initial_State_Should_Be_Used_For_All_Partitions()
    {
        var eventHub = new InMemoryEventHubProvider()
            .AddNamespace()
            .AddEventHub("test-eh", 2);

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);
        await using var consumer0 = InMemoryPartitionReceiver.FromEventHub("$default", "0", EventPosition.Earliest, eventHub);
        await using var consumer1 = InMemoryPartitionReceiver.FromEventHub("$default", "1", EventPosition.Earliest, eventHub);

        var sentEventData = new EventData();

        await producer.SendAsync([sentEventData], new SendEventOptions { PartitionId = "0" });
        await producer.SendAsync([sentEventData], new SendEventOptions { PartitionId = "1" });

        var batch0 = await consumer0.ReceiveBatchAsync(100, TimeSpan.Zero);
        var batch1 = await consumer1.ReceiveBatchAsync(100, TimeSpan.Zero);

        batch0.Should().HaveCount(1);
        batch1.Should().HaveCount(1);


        batch0.Single().SequenceNumber.Should().Be(0);
        batch0.Single().Offset.Should().Be(0);
        batch0.Single().EnqueuedTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2));

        batch1.Single().SequenceNumber.Should().Be(0);
        batch1.Single().Offset.Should().Be(0);
        batch1.Single().EnqueuedTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(2));

    }

}
