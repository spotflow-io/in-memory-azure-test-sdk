using System.Collections.Concurrent;

using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus;

namespace Tests.ServiceBus;

[TestClass]
public class ServiceBusSessionProcessorTests
{
    #region Initialization Tests

    [TestMethod]
    public async Task Constructor_WithQueue_SetsPropertiesCorrectly()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);

        const string queueName = "test-queue";
        var options = new ServiceBusSessionProcessorOptions { MaxConcurrentSessions = 5 };

        await using var processor = new InMemoryServiceBusSessionProcessor(client, queueName, options);

        processor.EntityPath.Should().Be(queueName);
        processor.MaxConcurrentSessions.Should().Be(5);
        processor.IsProcessing.Should().BeFalse();
        processor.IsClosed.Should().BeFalse();
    }

    [TestMethod]
    public async Task Constructor_WithSubscription_SetsPropertiesCorrectly()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);

        const string topicName = "test-topic";
        const string subscriptionName = "test-subscription";
        var options = new ServiceBusSessionProcessorOptions { MaxConcurrentSessions = 3 };

        await using var processor = new InMemoryServiceBusSessionProcessor(client, topicName, subscriptionName, options);

        processor.EntityPath.Should().Be($"{topicName}/subscriptions/{subscriptionName}");
        processor.MaxConcurrentSessions.Should().Be(3);
        processor.IsProcessing.Should().BeFalse();
        processor.IsClosed.Should().BeFalse();
    }

    #endregion

    #region Property Tests
    [TestMethod]
    public async Task ProcessorOptions_AreSetCorrectly()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new() { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);

        var options = new ServiceBusSessionProcessorOptions
        {
            MaxConcurrentSessions = 5,
            AutoCompleteMessages = false,
            PrefetchCount = 10,
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
            SessionIdleTimeout = TimeSpan.FromMinutes(2)
        };

        await using var processor = client.CreateSessionProcessor("test-queue", options);

        processor.MaxConcurrentSessions.Should().Be(5);
        processor.AutoCompleteMessages.Should().BeFalse();
        processor.PrefetchCount.Should().Be(10);
        processor.ReceiveMode.Should().Be(ServiceBusReceiveMode.ReceiveAndDelete);
        processor.SessionIdleTimeout.Should().Be(TimeSpan.FromMinutes(2));
        processor.EntityPath.Should().Be("test-queue");
        processor.FullyQualifiedNamespace.Should().Be(client.FullyQualifiedNamespace);
    }

    #endregion

    #region Lifecycle Tests

    [TestMethod]
    public async Task StartProcessingAsync_SetsIsProcessingToTrue()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new() { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor("test-queue");
        
        processor.ProcessMessageAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;
        
        await processor.StartProcessingAsync();

        processor.IsProcessing.Should().BeTrue();

        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task StartProcessingAsync_WhenAlreadyProcessing_ThrowsInvalidOperationException()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor("test-queue");
        
        processor.ProcessMessageAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;
        
        await processor.StartProcessingAsync();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => processor.StartProcessingAsync());

        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task StopProcessingAsync_SetsIsProcessingToFalse()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor("test-queue");
        
        processor.ProcessMessageAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;
        
        await processor.StartProcessingAsync();

        await processor.StopProcessingAsync();

        processor.IsProcessing.Should().BeFalse();
    }

    [TestMethod]
    public async Task StopProcessingAsync_WhenNotProcessing_DoesNotThrow()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor("test-queue");

        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task CloseAsync_SetsIsClosedToTrue()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateSessionProcessor("test-queue");

        await processor.CloseAsync();
        processor.IsClosed.Should().BeTrue();
    }

    [TestMethod]
    public async Task DisposeAsync_SetsIsClosedToTrue()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateSessionProcessor("test-queue");

        await processor.DisposeAsync();
        processor.IsClosed.Should().BeTrue();
    }

    [TestMethod]
    public async Task CloseAsync_WhenProcessing_StopProcessingFirst()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateSessionProcessor("test-queue");
        
        processor.ProcessMessageAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;
        
        await processor.StartProcessingAsync();

        await processor.CloseAsync();

        processor.IsProcessing.Should().BeFalse();
        processor.IsClosed.Should().BeTrue();
    }

    [TestMethod]
    public async Task StartProcessingAsync_AfterClose_ThrowsObjectDisposedException()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateSessionProcessor("test-queue");

        processor.ProcessMessageAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await processor.CloseAsync();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
            () => processor.StartProcessingAsync());
    }
    #endregion

    #region Session Message Processing Tests

    [TestMethod]
    public async Task ProcessSessionMessage_ReceivesAndProcessMessage()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new() { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor("test-queue");

        var messageReceived = new TaskCompletionSource<ProcessSessionMessageEventArgs>();
        processor.ProcessMessageAsync += args =>
        {
            messageReceived.TrySetResult(args);
            return Task.CompletedTask;
        };
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await using var sender = client.CreateSender("test-queue");
        await sender.SendMessageAsync(new ServiceBusMessage("Test Message") { SessionId = "session-1" });

        await processor.StartProcessingAsync();
        var receivedArgs = await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        receivedArgs.Message.Body.ToString().Should().Be("Test Message");
        receivedArgs.Message.SessionId.Should().Be("session-1");
        receivedArgs.SessionReceiver.SessionId.Should().Be("session-1");

        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task ProcessSessionMessage_WithAutoComplete_CompletesMessageAutomatically()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new() { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor("test-queue", new ServiceBusSessionProcessorOptions()
        {
            AutoCompleteMessages = true
        });
        
        var messageProcessed = new TaskCompletionSource<bool>();
        processor.ProcessMessageAsync += _ =>
        {
            messageProcessed.TrySetResult(true);
            return Task.CompletedTask;
        };
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        var sender = client.CreateSender("test-queue");
        await sender.SendMessageAsync(new ServiceBusMessage("Test Message") { SessionId = "session-1" });

        await processor.StartProcessingAsync();
        await messageProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        
        // Give some time for auto-completion
        await Task.Delay(100);
        var isCompleted = await WaitForMessageCompletion(client, "test-queue", "session-1", TimeSpan.FromSeconds(2));
        isCompleted.Should().BeTrue("message should be auto-completed and session should be empty");

        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task ProcessSessionMessage_WithManualComplete_DoesNotAutoComplete()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new() { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor("test-queue", new ServiceBusSessionProcessorOptions
        {
            AutoCompleteMessages = false
        });

        var messageProcessed = new TaskCompletionSource<bool>();
        processor.ProcessMessageAsync += async args =>
        {
            await args.CompleteMessageAsync(args.Message);
            messageProcessed.TrySetResult(true);
        };
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await using var sender = client.CreateSender("test-queue");
        await sender.SendMessageAsync(new ServiceBusMessage("Test Message") { SessionId = "session-1" });

        await processor.StartProcessingAsync();
        await messageProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var isCompleted = await WaitForMessageCompletion(client, "test-queue", "session-1", TimeSpan.FromSeconds(2));
        isCompleted.Should().BeTrue("message should be manually completed and session should be empty");

        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task ProcessSessionMessage_WithException_CallsErrorHandler()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new() { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor("test-queue");

        var errorHandled = new TaskCompletionSource<ProcessErrorEventArgs>();
        processor.ProcessMessageAsync += _ => throw new InvalidOperationException("Test exception");
        processor.ProcessErrorAsync += args =>
        {
            errorHandled.TrySetResult(args);
            return Task.CompletedTask;
        };

        await using var sender = client.CreateSender("test-queue");
        await sender.SendMessageAsync(new ServiceBusMessage("Test Message") { SessionId = "session-1" });

        await processor.StartProcessingAsync();
        var errorArgs = await errorHandled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        errorArgs.Exception.Should().BeOfType<InvalidOperationException>();
        errorArgs.Exception.Message.Should().Be("Test exception");
        errorArgs.ErrorSource.Should().Be(ServiceBusErrorSource.Receive);
        errorArgs.EntityPath.Should().Be("test-queue");

        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task ProcessSessionMessage_MultipleSessionsConcurrently()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new() { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor("test-queue", new ServiceBusSessionProcessorOptions
        {
            MaxConcurrentSessions = 3
        });

        var processedMessages = new ConcurrentBag<string>();
        var processedSessions = new ConcurrentBag<string>();
        var processingStarted = new TaskCompletionSource<bool>();
        var continueProcessing = new TaskCompletionSource<bool>();
        var messageCount = 0;

        processor.ProcessMessageAsync += async args =>
        {
            processedMessages.Add(args.Message.Body.ToString());
            processedSessions.Add(args.SessionReceiver.SessionId);

            if (Interlocked.Increment(ref messageCount) == 1)
            {
                processingStarted.TrySetResult(true);
            }
            await continueProcessing.Task;
        };
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await using var sender = client.CreateSender("test-queue");
        await sender.SendMessageAsync(new ServiceBusMessage("Message 1") { SessionId = "session-1" });
        await sender.SendMessageAsync(new ServiceBusMessage("Message 2") { SessionId = "session-2" });
        await sender.SendMessageAsync(new ServiceBusMessage("Message 3") { SessionId = "session-3" });

        await processor.StartProcessingAsync();
        await processingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        continueProcessing.TrySetResult(true);

        await Task.Delay(1000);

        processedMessages.Should().HaveCount(3);
        processedSessions.Should().HaveCount(3);
        processedSessions.Should().Contain("session-1");
        processedSessions.Should().Contain("session-2");
        processedSessions.Should().Contain("session-3");

        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task ProcessSessionMessage_FromSubscription_ReceivesTopicMessage()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        var subscription = ns.AddTopic("test-topic").AddSubscription("test-subscription", new() { EnableSessions = true });

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor(subscription.TopicName, subscription.SubscriptionName);

        var messageReceived = new TaskCompletionSource<ProcessSessionMessageEventArgs>();
        processor.ProcessMessageAsync += args =>
        {
            messageReceived.TrySetResult(args);
            return Task.CompletedTask;
        };
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await using var sender = client.CreateSender("test-topic");
        await sender.SendMessageAsync(new ServiceBusMessage("Topic message") { SessionId = "topic-session" });

        await processor.StartProcessingAsync();
        var receivedArgs = await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        receivedArgs.Message.Body.ToString().Should().Be("Topic message");
        receivedArgs.Message.SessionId.Should().Be("topic-session");

        await processor.StopProcessingAsync();
    }

    #endregion

    #region Handlers
    [TestMethod]
    public async Task ProcessMessageAsync_AddSecondHandler_ThrowsNotSupportedException()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new() { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateSessionProcessor("test-queue");

        processor.ProcessMessageAsync += _ => Task.CompletedTask;

        Assert.ThrowsException<NotSupportedException>(() =>
        {
            processor.ProcessMessageAsync += _ => Task.CompletedTask;
        });
        await processor.CloseAsync();
    }

    [TestMethod]
    public async Task RemoveHandler_ThenAddNew_ShouldWork()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new() { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor("test-queue");

        Func<ProcessSessionMessageEventArgs, Task> handler1 = _ => Task.CompletedTask;
        Func<ProcessSessionMessageEventArgs, Task> handler2 = _ => Task.CompletedTask;

        processor.ProcessMessageAsync += handler1;
        processor.ProcessMessageAsync -= handler1;
        processor.ProcessMessageAsync += handler2;
    }
    #endregion

    #region Helper Methods
    private static async Task<bool> WaitForMessageCompletion(InMemoryServiceBusClient client, string queueName, string sessionId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var sessionReceiver = await client.AcceptSessionAsync(queueName, sessionId);
                var messages = await sessionReceiver.ReceiveMessagesAsync(1, TimeSpan.FromMilliseconds(100));

                if (messages.Count == 0)
                {
                    return true;
                }
            }
            catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.ServiceTimeout)
            {
                // Session might have been released or closed
                return true;
            }
            
            await Task.Delay(50);
        }
        return false;
    }
    #endregion

    [TestMethod]
    public async Task StopProcessing_WaitsForInFlightMessages()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new() { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor("test-queue");

        var messageStarted = new TaskCompletionSource<bool>();
        var messageCanComplete = new TaskCompletionSource<bool>();
        var messageCompleted = new TaskCompletionSource<bool>();

        processor.ProcessMessageAsync += async _ =>
        {
            messageStarted.TrySetResult(true);
            await messageCanComplete.Task; // block here until it is ok to continue
            messageCompleted.TrySetResult(true);
        };
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await using var sender = client.CreateSender("test-queue");
        await sender.SendMessageAsync(new ServiceBusMessage("Test") { SessionId = "test-session" });

        await processor.StartProcessingAsync();
        await messageStarted.Task;

        // Start stopping while message is in flight
        var stopTask = processor.StopProcessingAsync();

        // Verify stop is waiting for message completion
        await Task.Delay(100);
        stopTask.IsCompleted.Should().BeFalse();

        messageCanComplete.TrySetResult(true);
        await messageCompleted.Task;
        await stopTask;
        processor.IsProcessing.Should().BeFalse();
    }

    [TestMethod]
    public async Task ConcurrentCloseAndStart_HandlesGracefully()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new() { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateSessionProcessor("test-queue");

        processor.ProcessMessageAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        var closeTask = Task.Run(() => processor.CloseAsync());
        var startTask = Task.Run(async () =>
        {
            try
            {
                await processor.StartProcessingAsync();
            }
            catch (ObjectDisposedException)
            {
                // Expected if close operation is successful
            }
        });

        await Task.WhenAll(closeTask, startTask);
        processor.IsClosed.Should().BeTrue();
    }
}