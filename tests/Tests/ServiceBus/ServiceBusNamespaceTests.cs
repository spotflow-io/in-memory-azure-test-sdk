using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus;

namespace Tests.ServiceBus;

[TestClass]
public class ServiceBusNamespaceTests
{
    [TestMethod]
    public void Connection_String_Should_Be_Returned()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        var connectionString = ns.GetConnectionString();

        var connection = ServiceBusConnectionStringProperties.Parse(connectionString);

        connection.FullyQualifiedNamespace.Should().Be(ns.FullyQualifiedNamespace);
        connection.EntityPath.Should().Be(null);
    }

    [TestMethod]
    public void Namespace_Should_Be_Case_Insensitive()
    {
        var provider = new InMemoryServiceBusProvider();

        var ns = provider.AddNamespace("TestNamespace");

        provider.GetNamespace("testnamespace").Should().BeSameAs(ns);
        provider.GetNamespace("TESTNAMESPACE").Should().BeSameAs(ns);
        provider
            .GetFullyQualifiedNamespace("testnamespace.servicebus.in-memory.example.com")
            .Should()
            .BeSameAs(ns);

    }
}

