using Azure.Messaging.EventHubs;

using Spotflow.InMemory.Azure.EventHubs;

namespace Tests.EventHub;

[TestClass]
public class EventHubNamespaceTests
{
    [TestMethod]
    public void ConnectionString_ShouldBeReturned()
    {
        var eventHubNamespace = new InMemoryEventHubProvider().AddNamespace();
        var connectionString = eventHubNamespace.CreateConnectionString();

        var connection = new EventHubConnection(connectionString, "test-eh");

        connection.FullyQualifiedNamespace.Should().Be(eventHubNamespace.FullyQualifiedNamespace);
        connection.EventHubName.Should().Be("test-eh");
    }
}
