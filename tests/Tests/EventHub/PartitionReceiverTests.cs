using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
using Azure.Messaging.EventHubs.Producer;

using Microsoft.Extensions.Time.Testing;

using Spotflow.InMemory.Azure.EventHubs;

using Tests.Utils;

namespace Tests.EventHub;

[TestClass]
public class PartitionReceiverTests
{
    [TestMethod]
    public async Task Constructor_With_Connection_String_Should_Succeed()
    {
        var provider = new InMemoryEventHubProvider();

        var eventHub = provider.AddNamespace().AddEventHub("test", 2);

        var connectionString = eventHub.CreateConnectionString();

        await using var client = new InMemoryPartitionReceiver("cg", "pid", EventPosition.Earliest, connectionString, provider);

        client.EventHubName.Should().Be("test");
        client.FullyQualifiedNamespace.Should().Be(eventHub.Namespace.FullyQualifiedNamespace);
        client.Identifier.Should().NotBeNullOrWhiteSpace();
        client.IsClosed.Should().BeFalse();
        client.ConsumerGroup.Should().Be("cg");
        client.InitialPosition.Should().Be(EventPosition.Earliest);
        client.PartitionId.Should().Be("pid");
    }

    [TestMethod]
    public async Task SpecificStartingPosition_Inclusive_ShouldReturnOnlySpecificEvents()
    {
        var eventHub = new InMemoryEventHubProvider()
            .AddNamespace()
            .AddEventHub("test-eh", 1)
            .AddConsumerGroup("test-cg");

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);
        await using var receiver = InMemoryPartitionReceiver.FromEventHub("test-cg", "0", EventPosition.FromSequenceNumber(1, isInclusive: true), eventHub);

        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-0"))]);
        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-1"))]);
        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-2"))]);
        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-3"))]);

        var batch = await receiver.ReceiveBatchAsync(100);

        batch.Select(e => e.EventBody.ToString()).Should().Equal(["test-data-1", "test-data-2", "test-data-3"]);

    }

    [TestMethod]
    public async Task Subsequent_Receive_Should_Return_Empty_Batch_If_All_Existing_Events_Are_Received()
    {
        var eventHub = new InMemoryEventHubProvider()
            .AddNamespace()
            .AddEventHub("test-eh", 1)
            .AddConsumerGroup("test-cg");

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);
        await using var receiver = InMemoryPartitionReceiver.FromEventHub("test-cg", "0", EventPosition.Earliest, eventHub);

        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-0"))]);

        var batch1 = await receiver.ReceiveBatchAsync(100);

        batch1.Select(e => e.EventBody.ToString()).Should().Equal(["test-data-0"]);

        var batch2 = await receiver.ReceiveBatchAsync(1, TimeSpan.FromSeconds(1));

        batch2.Should().BeEmpty();

    }

    [TestMethod]
    public async Task SpecificStartingPosition_Exclusive_ShouldReturnOnlySpecificEvents()
    {
        var eventHub = new InMemoryEventHubProvider()
            .AddNamespace()
            .AddEventHub("test-eh", 1)
            .AddConsumerGroup("test-cg");

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);

        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-0"))]);
        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-1"))]);
        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-2"))]);
        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-3"))]);

        await using var receiver = InMemoryPartitionReceiver.FromEventHub("test-cg", "0", EventPosition.FromSequenceNumber(1, isInclusive: false), eventHub);

        var batch = await receiver.ReceiveBatchAsync(100);

        batch.Select(e => e.EventBody.ToString()).Should().Equal(["test-data-2", "test-data-3"]);

    }


    [TestMethod]
    public async Task LatestStartingPosition_ShouldReturnOnlyNewEvents()
    {
        var eventHub = new InMemoryEventHubProvider()
            .AddNamespace()
            .AddEventHub("test-eh", 1)
            .AddConsumerGroup("test-cg");

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);

        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-1"))]);

        await using var receiver = InMemoryPartitionReceiver.FromEventHub("test-cg", "0", EventPosition.Latest, eventHub);

        var batch1 = await receiver.ReceiveBatchAsync(100, TimeSpan.Zero);

        batch1.Should().BeEmpty();

        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-2"))]);

        var batch2 = await receiver.ReceiveBatchAsync(100, TimeSpan.Zero);

        batch2.Should().ContainSingle(e => e.EventBody.ToString() == "test-data-2");

    }

    [TestMethod]
    public async Task EarliestStartingPosition_ShouldReturnAllEvents()
    {
        var eventHub = new InMemoryEventHubProvider()
            .AddNamespace()
            .AddEventHub("test-eh", 1)
            .AddConsumerGroup("test-cg");

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);

        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-1"))]);

        await using var receiver = InMemoryPartitionReceiver.FromEventHub("test-cg", "0", EventPosition.Earliest, eventHub);

        var batch1 = await receiver.ReceiveBatchAsync(100, TimeSpan.Zero);

        batch1.Should().ContainSingle(e => e.EventBody.ToString() == "test-data-1");

        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-2"))]);

        var batch2 = await receiver.ReceiveBatchAsync(100, TimeSpan.Zero);

        batch2.Should().ContainSingle(e => e.EventBody.ToString() == "test-data-2");

    }

    [TestMethod]
    public async Task By_Default_There_Should_Be_Default_Consumer_Group_And_Position_Is_Set_To_Earliest()
    {
        var eventHub = new InMemoryEventHubProvider()
            .AddNamespace()
            .AddEventHub("test-eh", 1);

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);

        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-1"))]);

        await using var receiver = InMemoryPartitionReceiver.FromEventHub("0", EventPosition.Earliest, eventHub);

        var batch1 = await receiver.ReceiveBatchAsync(100, TimeSpan.Zero);

        batch1.Should().ContainSingle(e => e.EventBody.ToString() == "test-data-1");

        await producer.SendAsync([new EventData(BinaryData.FromString("test-data-2"))]);

        var batch2 = await receiver.ReceiveBatchAsync(100, TimeSpan.Zero);

        batch2.Should().ContainSingle(e => e.EventBody.ToString() == "test-data-2");

    }

    [TestMethod]
    public async Task Last_Enqueued_Event_Properties_Should_Be_Refreshed()
    {
        var timeProvider = new FakeTimeProvider();

        var eventHub = new InMemoryEventHubProvider(timeProvider: timeProvider)
            .AddNamespace()
            .AddEventHub("test-eh", 1);

        await using var receiver = InMemoryPartitionReceiver.FromEventHub("0", EventPosition.Earliest, eventHub);

        // Initial LastEnqueuedEventProperties shuold be default

        receiver.ReadLastEnqueuedEventProperties().Should().Be(default(LastEnqueuedEventProperties));

        // Receive several empty batches which should yield default LastEnqueuedEventProperties.

        for (var i = 0; i < 16; i++)
        {
            var batch0Task = receiver.ReceiveBatchAsync(100, TimeSpan.FromSeconds(16));

            while (!batch0Task.IsCompleted)
            {
                timeProvider.Advance(TimeSpan.FromSeconds(4));
            }

            var batch0 = await batch0Task;

            batch0.Should().BeEmpty();

            var properties0 = receiver.ReadLastEnqueuedEventProperties();

            properties0.Should().NotBeNull();
            properties0.Should().Be(default(LastEnqueuedEventProperties));

        }

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);
        var data = BinaryData.FromString("test-data-1");

        // Send first event

        await producer.SendAsync([new EventData(data)]);

        // Properties after first event without receiving should remain default.

        receiver.ReadLastEnqueuedEventProperties().Should().Be(default(LastEnqueuedEventProperties));

        // Refresh partition properties by receiving & check properties

        _ = await receiver.ReceiveBatchAsync(100, TimeSpan.Zero);

        var properties1 = receiver.ReadLastEnqueuedEventProperties();
        properties1.SequenceNumber.Should().Be(0);
        properties1.Offset.Should().Be(0);


        // Send second event
        await producer.SendAsync([new EventData(data)]);

        // Properties after second event without receiving should be same as previously.

        var properties2 = receiver.ReadLastEnqueuedEventProperties();
        properties2.SequenceNumber.Should().Be(0);
        properties2.Offset.Should().Be(0);

        _ = await receiver.ReceiveBatchAsync(100, TimeSpan.Zero);

        // Refresh partition properties by receiving & check properties

        var properties3 = receiver.ReadLastEnqueuedEventProperties();

        properties3.SequenceNumber.Should().Be(1);
        properties3.Offset.Should().Be(37);

    }

    [TestMethod]
    public async Task Trimmed_Partition_Events_Should_Not_Be_Received()
    {
        var provider = new InMemoryEventHubProvider();

        var eventHub = provider.AddNamespace().AddEventHub("test", 1);

        await using var producer = InMemoryEventHubProducerClient.FromEventHub(eventHub);

        foreach (var i in Enumerable.Range(0, 100))
        {
            await producer.SendAsync([new EventData()], new SendEventOptions { PartitionId = "0" });
        }

        await using (var consumer = InMemoryPartitionReceiver.FromEventHub("0", EventPosition.Earliest, eventHub))
        {
            var batch = await consumer.ReceiveBatchAsync(2, TimeSpan.Zero);

            batch.ElementAt(0).SequenceNumber.Should().Be(0);
            batch.ElementAt(1).SequenceNumber.Should().Be(1);
        }

        eventHub.TriggerRetentionPolicy("0", 42);

        await using (var consumer = InMemoryPartitionReceiver.FromEventHub("0", EventPosition.Earliest, eventHub))
        {
            var batch = await consumer.ReceiveBatchAsync(2, TimeSpan.Zero);

            batch.ElementAt(0).SequenceNumber.Should().Be(42);
            batch.ElementAt(1).SequenceNumber.Should().Be(43);
        }

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Starting_Position_Higher_Than_Latest_Event_Should_Fail()
    {
        var inMemoryProvider = new InMemoryEventHubProvider();
        var inMemoryEventHub = inMemoryProvider.AddNamespace().AddEventHub("previously-used-active", 1);

        var events = Enumerable.Range(0, 128).Select(i => new EventData());

        await using var producerClient = await ImplementationProvider.GetEventHubProducerClientAsync(inMemoryEventHub);

        await producerClient.SendAsync(events);

        await using var consumerClient = await ImplementationProvider.GetEventHubConsumerClientAsync(inMemoryEventHub);

        var partitionProperties = await consumerClient.GetPartitionPropertiesAsync("0");

        var lastEnqueuedSequenceNumber = partitionProperties.LastEnqueuedSequenceNumber;

        lastEnqueuedSequenceNumber.Should().BeGreaterThanOrEqualTo(64, "otherwise test is not meaningful");

        var startingPositionSequenceNumber = lastEnqueuedSequenceNumber + 10_000;

        var startingPosition = EventPosition.FromSequenceNumber(startingPositionSequenceNumber);

        await using var receiver = await ImplementationProvider.GetEventHubPartitionReceiverAsync("0", startingPosition, inMemoryEventHub);

        var act = () => receiver.ReceiveBatchAsync(100);

        var expectedMessage = $"" +
            $"The supplied sequence number '{startingPositionSequenceNumber}' is invalid. " +
            $"The last sequence number in the system is '{lastEnqueuedSequenceNumber}'";

        await act
            .Should()
            .ThrowAsync<ArgumentException>()
            .Where(e => e.Message.StartsWith(expectedMessage));
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Starting_Position_As_Last_Sequence_Number_Should_Succeed_As_Inclusive_And_Exclusive()
    {
        var inMemoryProvider = new InMemoryEventHubProvider();
        var inMemoryEventHub = inMemoryProvider.AddNamespace().AddEventHub("previously-used-active", 1);

        var events = Enumerable.Range(0, 128).Select(i => new EventData());

        await using (var producerClient = await ImplementationProvider.GetEventHubProducerClientAsync(inMemoryEventHub))
        {
            await producerClient.SendAsync(events);
        }

        await using var consumerClient = await ImplementationProvider.GetEventHubConsumerClientAsync(inMemoryEventHub);

        var partitionProperties = await consumerClient.GetPartitionPropertiesAsync("0");

        var lastEnqueuedSequenceNumber = partitionProperties.LastEnqueuedSequenceNumber;

        lastEnqueuedSequenceNumber.Should().BeGreaterThanOrEqualTo(64, "otherwise test is not meaningful");

        var startingPositionSequenceNumber = lastEnqueuedSequenceNumber;

        var startingPositionInclusive = EventPosition.FromSequenceNumber(startingPositionSequenceNumber, isInclusive: true);

        await using (var receiver = await ImplementationProvider.GetEventHubPartitionReceiverAsync("0", startingPositionInclusive, inMemoryEventHub))
        {
            var batch = await receiver.ReceiveBatchAsync(100, TimeSpan.FromMilliseconds(100));
            batch.Should().ContainSingle(e => e.SequenceNumber == lastEnqueuedSequenceNumber);
        }

        var startingPositionExclusive = EventPosition.FromSequenceNumber(startingPositionSequenceNumber, isInclusive: false);

        await using (var receiver = await ImplementationProvider.GetEventHubPartitionReceiverAsync("0", startingPositionExclusive, inMemoryEventHub))
        {
            var batch = await receiver.ReceiveBatchAsync(100, TimeSpan.Zero);
            batch.Should().BeEmpty();
        }

    }


    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Starting_Position_Lower_Than_Beginning_Event_Should_Return_First_Available_Event()
    {
        var inMemoryProvider = new InMemoryEventHubProvider();
        var inMemoryEventHub = inMemoryProvider.AddNamespace().AddEventHub("previously-used-active", 1);

        await using var producerClient = await ImplementationProvider.GetEventHubProducerClientAsync(inMemoryEventHub);

        var events = Enumerable.Range(0, 128).Select(i => new EventData());

        await producerClient.SendAsync(events);

        inMemoryEventHub.TriggerRetentionPolicy("0", 42);

        await using var consumerClient = await ImplementationProvider.GetEventHubConsumerClientAsync(inMemoryEventHub);

        var partitionProperties = await consumerClient.GetPartitionPropertiesAsync("0");

        var lastEnqueuedSequenceNumber = partitionProperties.LastEnqueuedSequenceNumber;

        lastEnqueuedSequenceNumber.Should().BeGreaterThanOrEqualTo(64, "otherwise test is not meaningful");

        var beginningSequenceNumber = partitionProperties.BeginningSequenceNumber;

        beginningSequenceNumber.Should().BeGreaterThanOrEqualTo(42, "otherwise test is not meaningful");

        var startingPositionSequenceNumber = beginningSequenceNumber - 10;

        var startingPosition = EventPosition.FromSequenceNumber(startingPositionSequenceNumber);

        await using var receiver = await ImplementationProvider.GetEventHubPartitionReceiverAsync("0", startingPosition, inMemoryEventHub);

        var batch = await receiver.ReceiveBatchAsync(1);

        batch.Should().HaveCount(1);
        batch.Single().SequenceNumber.Should().Be(beginningSequenceNumber);
    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Non_Initial_Starting_Position_For_Previously_Used_But_Currently_Empty_Event_Hub_Should_Succeed_For_Earlier_Or_Equal_Position_And_Fail_For_Later_Position()
    {
        var inMemoryProvider = new InMemoryEventHubProvider();
        var inMemoryEventHub = inMemoryProvider.AddNamespace().AddEventHub("previously-used-empty", 1);

        if (!ImplementationProvider.IsAzureConfigAvailable)
        {
            await using var producerClient = InMemoryEventHubProducerClient.FromEventHub(inMemoryEventHub);
            var events = Enumerable.Range(0, 128).Select(i => new EventData());
            await producerClient.SendAsync(events);
            inMemoryEventHub.TriggerRetentionPolicy("0", 128);
        }

        await using var consumerClient = await ImplementationProvider.GetEventHubConsumerClientAsync(inMemoryEventHub);

        var partitionProperties = await consumerClient.GetPartitionPropertiesAsync("0");

        var beginningSequenceNumber = partitionProperties.BeginningSequenceNumber;
        var lastEnqueuedSequenceNumber = partitionProperties.LastEnqueuedSequenceNumber;

        partitionProperties.IsEmpty.Should().BeTrue("otherwise test is not meaningful");
        lastEnqueuedSequenceNumber.Should().BeGreaterThanOrEqualTo(64, "otherwise test is not meaningful");
        beginningSequenceNumber.Should().Be(lastEnqueuedSequenceNumber);

        var earlyStartingPosition = EventPosition.FromSequenceNumber(beginningSequenceNumber - 10);
        var equalStartingPosition = EventPosition.FromSequenceNumber(beginningSequenceNumber);
        var laterStartingPosition = EventPosition.FromSequenceNumber(lastEnqueuedSequenceNumber + 10);

        await using var earlyReceiver = await ImplementationProvider.GetEventHubPartitionReceiverAsync("0", earlyStartingPosition, inMemoryEventHub);
        var earlyBatch = await earlyReceiver.ReceiveBatchAsync(100, TimeSpan.FromSeconds(1));
        earlyBatch.Should().BeEmpty();

        await using var equalReceiver = await ImplementationProvider.GetEventHubPartitionReceiverAsync("0", equalStartingPosition, inMemoryEventHub);
        var equalBatch = await equalReceiver.ReceiveBatchAsync(100, TimeSpan.FromSeconds(1));
        equalBatch.Should().BeEmpty();

        await using var laterReceiver = await ImplementationProvider.GetEventHubPartitionReceiverAsync("0", laterStartingPosition, inMemoryEventHub);

        var act = () => laterReceiver.ReceiveBatchAsync(100, TimeSpan.FromSeconds(1));

        var expectedMessage = $"" +
            $"The supplied sequence number '{lastEnqueuedSequenceNumber + 10}' is invalid. " +
            $"The last sequence number in the system is '{lastEnqueuedSequenceNumber}'";

        await act
            .Should()
            .ThrowAsync<ArgumentException>()
            .Where(e => e.Message.StartsWith(expectedMessage));

    }

    [TestMethod]
    [TestCategory(TestCategory.AzureInfra)]
    public async Task Starting_Position_For_New_Event_Hub_Should_Be_Minus1_Both_Inclusive_And_Exlusive_Position_Should_Succeed()
    {
        var inMemoryProvider = new InMemoryEventHubProvider();
        var inMemoryEventHub = inMemoryProvider.AddNamespace().AddEventHub("not-used", 1);

        await using var consumerClient = await ImplementationProvider.GetEventHubConsumerClientAsync(inMemoryEventHub);

        var partitionProperties = await consumerClient.GetPartitionPropertiesAsync("0");

        var lastEnqueuedSequenceNumber = partitionProperties.LastEnqueuedSequenceNumber;

        lastEnqueuedSequenceNumber.Should().Be(-1, "othewise test is not meaningful.");

        var startingPositionInclusive = EventPosition.FromSequenceNumber(-1, isInclusive: true);

        await using (var receiver = await ImplementationProvider.GetEventHubPartitionReceiverAsync("0", startingPositionInclusive, inMemoryEventHub))
        {
            var batch = await receiver.ReceiveBatchAsync(1, TimeSpan.Zero);

            batch.Should().BeEmpty();
        }

        var startingPositionExclusive = EventPosition.FromSequenceNumber(-1, isInclusive: false);

        await using (var receiver = await ImplementationProvider.GetEventHubPartitionReceiverAsync("0", startingPositionExclusive, inMemoryEventHub))
        {
            var batch = await receiver.ReceiveBatchAsync(1, TimeSpan.Zero);

            batch.Should().BeEmpty();
        }
    }

}
