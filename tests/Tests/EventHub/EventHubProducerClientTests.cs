using System.Text;

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

    [TestMethod]
    public async Task Offset_Should_Start_At_Zero_And_Increase_With_Each_Sent_Event()
    {
        var eventHub = new InMemoryEventHubProvider().AddNamespace().AddEventHub("test-eh", 1);

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);
        await using var consumer = InMemoryPartitionReceiver.FromEventHub("0", EventPosition.Earliest, eventHub);

        var emptyEvent = new EventData();
        var eventWithBody = new EventData(Encoding.UTF8.GetBytes("test-body"));

        await producer.SendAsync([emptyEvent]);
        var emptyEventBatch = await consumer.ReceiveBatchAsync(1, TimeSpan.Zero);
        emptyEventBatch.Single().Offset.Should().Be(0);

        await producer.SendAsync([emptyEvent], new SendEventOptions { PartitionKey = "test-pk" });
        var emptyEventWithPartitionKey = await consumer.ReceiveBatchAsync(1, TimeSpan.Zero);
        emptyEventWithPartitionKey.Single().Offset.Should().Be(26);

        await producer.SendAsync([eventWithBody]);
        var eventWithBodyBatch = await consumer.ReceiveBatchAsync(1, TimeSpan.Zero);
        eventWithBodyBatch.Single().Offset.Should().Be(59);
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(1024)]
    [DataRow(1237)]
    public async Task Send_With_Partition_Key_Should_Provide_Distribution_Properties(int partitionCount)
    {
        var eventHub = new InMemoryEventHubProvider().AddNamespace().AddEventHub("test-eh", partitionCount);

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);

        foreach (var i in Enumerable.Range(0, 100_000))
        {
            var options = new CreateBatchOptions { PartitionKey = $"test-pk-{i:D10}" };

            var batch = await producer.CreateBatchAsync(options);

            batch.TryAdd(new EventData());

            await producer.SendAsync(batch);
        }

        var lastSequenceNumbers = eventHub.GetPartitionProperties().Select(kv => kv.Value.LastEnqueuedSequenceNumber).ToList();

        var averageLastSequenceNumber = (long) lastSequenceNumbers.Average();

        var delta = (ulong) (averageLastSequenceNumber * 0.4);

        foreach (var sequenceNumber in lastSequenceNumbers)
        {
            sequenceNumber.Should().BeCloseTo(averageLastSequenceNumber, delta);
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(1024)]
    [DataRow(1237)]
    public async Task Send_With_Partition_Key_Should_Respect_Partitioning_Invariant(int partitionCount)
    {
        var eventHub = new InMemoryEventHubProvider().AddNamespace().AddEventHub("test-eh", partitionCount);

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);

        foreach (var i in Enumerable.Range(0, 10_000))
        {
            var options = new CreateBatchOptions { PartitionKey = $"test-pk" };

            var batch = await producer.CreateBatchAsync(options);

            batch.TryAdd(new EventData());

            await producer.SendAsync(batch);
        }

        var lastSequenceNumbers = eventHub.GetPartitionProperties().Select(kv => kv.Value.LastEnqueuedSequenceNumber).ToList();

        lastSequenceNumbers.Where(n => n >= 0).Should().HaveCount(1);
        lastSequenceNumbers.Where(n => n == -1).Should().HaveCount(partitionCount - 1);

    }

}
