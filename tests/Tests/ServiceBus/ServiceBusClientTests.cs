using System.Text.RegularExpressions;

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

        await using var client = new InMemoryServiceBusClient(queue.Namespace.GetConnectionString(), serviceBusProvider);

        client.FullyQualifiedNamespace.Should().Be(queue.Namespace.FullyQualifiedNamespace);
        client.IsClosed.Should().BeFalse();
        client.TransportType.Should().Be(ServiceBusTransportType.AmqpTcp);
        client.Identifier.Should().NotBeNullOrWhiteSpace();
    }

    [TestMethod]
    public async Task Identifier_Can_Be_Provided_Explicitly()
    {
        var serviceBusProvider = new InMemoryServiceBusProvider();
        var queue = serviceBusProvider.AddNamespace().AddQueue("my-queue");
        var options = new ServiceBusClientOptions
        {
            Identifier = "some-identifier",
        };

        await using var client = new InMemoryServiceBusClient(queue.Namespace.GetConnectionString(), options, serviceBusProvider);

        client.Identifier.Should().Be("some-identifier");
    }

    [TestMethod]
    public async Task Identifier_Is_Generated_From_Namespace()
    {
        var serviceBusProvider = new InMemoryServiceBusProvider();
        var queue = serviceBusProvider.AddNamespace().AddQueue("my-queue");

        await using var client = new InMemoryServiceBusClient(queue.Namespace.GetConnectionString(), serviceBusProvider);

        client.Identifier.Should().StartWith($"{client.FullyQualifiedNamespace}-");
        client.Identifier.Should().MatchRegex(ServiceBusClientIdentifierRegex.IdentifierRegex());
    }
}
