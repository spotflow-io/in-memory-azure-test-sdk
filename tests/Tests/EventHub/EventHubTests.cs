using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;

using Spotflow.InMemory.Azure.EventHubs;

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
    [DataRow(123, null, null, 985, 909, DisplayName = "Seed 123")]
    [DataRow(456, null, null, 953, 620, DisplayName = "Seed 456")]
    [DataRow(456, null, 200000, 190438, 123247, DisplayName = "Seed 456 with max value")]
    [DataRow(null, null, null, 474, 57, DisplayName = "Default seed")]
    [DataRow(null, null, 200000, 93786, 9642, DisplayName = "Default seed with max value")]
    [DataRow(42, 32, 38, 36, 32, DisplayName = "Seed 42 with min and max value")]
    [DataRow(42, null, 12, 12, 10, DisplayName = "Seed 42 with max value close to min value")]
    public async Task InitialSequenceNumbers_WithRandomization_WithSeed_ShouldBeUsed(int? seed, int? min, int? max, int expectedSequenceNumberForPartition0, int expectedSequenceNumberForPartition1)
    {
        var eventHub = new InMemoryEventHubProvider()
            .AddNamespace()
            .AddEventHub("test-eh", 2, options =>
            {
                options.RandomizeInitialSequenceNumbers = true;

                if (seed.HasValue)
                {
                    options.RandomizationSeed = seed;
                }

                if (min.HasValue)
                {
                    options.MinRandomInitialSequenceNumber = min.Value;
                }

                if (max.HasValue)
                {
                    options.MaxRandomInitialSequenceNumber = max.Value;
                }

            });

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);
        await using var consumer0 = InMemoryPartitionReceiver.FromEventHub("$default", "0", EventPosition.Earliest, eventHub);
        await using var consumer1 = InMemoryPartitionReceiver.FromEventHub("$default", "1", EventPosition.Earliest, eventHub);

        var sentEventData = new EventData();

        await producer.SendAsync([sentEventData], new SendEventOptions { PartitionId = "0" });
        await producer.SendAsync([sentEventData], new SendEventOptions { PartitionId = "1" });

        var batch0 = await consumer0.ReceiveBatchAsync(100, TimeSpan.Zero);
        var batch1 = await consumer1.ReceiveBatchAsync(100, TimeSpan.Zero);

        batch0.Should().ContainSingle().Which.SequenceNumber.Should().Be(expectedSequenceNumberForPartition0);
        batch1.Should().ContainSingle().Which.SequenceNumber.Should().Be(expectedSequenceNumberForPartition1);

        eventHub.GetInitialSequenceNumber("0").Should().Be(expectedSequenceNumberForPartition0);
        eventHub.GetInitialSequenceNumber("1").Should().Be(expectedSequenceNumberForPartition1);
    }

    [TestMethod]

    public async Task InitialSequenceNumbers_WithRandomization_WithoutSeed_ShouldBeUsed()
    {
        var eventHub = new InMemoryEventHubProvider()
            .AddNamespace()
            .AddEventHub("test-eh", 1, options =>
            {
                options.RandomizeInitialSequenceNumbers = true;
                options.RandomizationSeed = null;
            });

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);
        await using var consumer = InMemoryPartitionReceiver.FromEventHub("$default", "0", EventPosition.Earliest, eventHub);

        const int eventsCount = 100_000;

        foreach (var i in Enumerable.Range(0, eventsCount))
        {
            await producer.SendAsync([new EventData()], new SendEventOptions { PartitionId = "0" });
        }

        var batch = await consumer.ReceiveBatchAsync(100, TimeSpan.Zero);

        batch.Should().AllSatisfy(e => e.SequenceNumber.Should().BeInRange(1, 1900 + eventsCount));


        eventHub.GetInitialSequenceNumber("0").Should().BeInRange(1, 1000 + eventsCount);


    }

    [TestMethod]

    public async Task InitialSequenceNumbers_ShouldBeZero()
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

        batch0.Should().ContainSingle().Which.SequenceNumber.Should().Be(0);
        batch1.Should().ContainSingle().Which.SequenceNumber.Should().Be(0);

        eventHub.GetInitialSequenceNumber("0").Should().Be(0);
        eventHub.GetInitialSequenceNumber("1").Should().Be(0);


    }

}
