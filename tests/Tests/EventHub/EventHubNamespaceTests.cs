using Azure.Messaging.EventHubs;

using Spotflow.InMemory.Azure.EventHubs;

namespace Tests.EventHub;

[TestClass]
public class EventHubNamespaceTests
{
    [TestMethod]
    public void ConnectionString_ShouldBeReturned()
    {
        var ns = new InMemoryEventHubProvider().AddNamespace();
        var connectionString = ns.GetConnectionString();

        var connection = new EventHubConnection(connectionString, "test-eh");

        connection.FullyQualifiedNamespace.Should().Be(ns.FullyQualifiedNamespace);
        connection.EventHubName.Should().Be("test-eh");
    }

    [TestMethod]
    public void Namespace_Should_Be_Case_Insensitive()
    {
        var provider = new InMemoryEventHubProvider();

        var ns = provider.AddNamespace("TestNamespace");

        provider.GetNamespace("testnamespace").Should().BeSameAs(ns);
        provider.GetNamespace("TESTNAMESPACE").Should().BeSameAs(ns);
        provider
            .GetFullyQualifiedNamespace("testnamespace.eventhub.in-memory.example.com")
            .Should()
            .BeSameAs(ns);

    }
}
