using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Consumer;
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

        var connectionString = eventHub.CreateConnectionString();

        await using var client = new InMemoryEventHubConsumerClient("cg", connectionString, provider);

        client.EventHubName.Should().Be("test");
        client.FullyQualifiedNamespace.Should().Be(eventHub.Namespace.FullyQualifiedNamespace);
        client.Identifier.Should().NotBeNullOrWhiteSpace();
        client.IsClosed.Should().BeFalse();
        client.ConsumerGroup.Should().Be("cg");
    }
}
