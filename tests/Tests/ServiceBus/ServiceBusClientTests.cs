using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus;

namespace Tests.ServiceBus;

[TestClass]

public class ServiceBusClientTests
{
    [TestMethod]
    public async Task Constructor_With_Connection_String_Should_Succeed()
    {
        var serviceBusProvider = new InMemoryServiceBusProvider();
        var queue = serviceBusProvider.AddNamespace().AddQueue("my-queue");

        await using var client = new InMemoryServiceBusClient(queue.Namespace.CreateConnectionString(), serviceBusProvider);

        client.FullyQualifiedNamespace.Should().Be(queue.Namespace.FullyQualifiedNamespace);
        client.IsClosed.Should().BeFalse();
        client.TransportType.Should().Be(ServiceBusTransportType.AmqpTcp);
        client.Identifier.Should().NotBeNullOrWhiteSpace();
    }
}
