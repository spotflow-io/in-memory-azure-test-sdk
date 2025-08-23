using System.Collections.Concurrent;

using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus;

namespace Tests.ServiceBus;

[TestClass]
public class ServiceBusProcessorTests
{
    #region Initialization Tests

    [TestMethod]
    public async Task Constructor_WithQueue_SetsPropertiesCorrectly()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);

        const string queueName = "test-queue";
        var options = new ServiceBusProcessorOptions { MaxConcurrentCalls = 5 };

        await using var processor = new InMemoryServiceBusProcessor(client, queueName, options);

        processor.EntityPath.Should().Be(queueName);
        processor.MaxConcurrentCalls.Should().Be(5);
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
        var options = new ServiceBusProcessorOptions { MaxConcurrentCalls = 3 };

        await using var processor = new InMemoryServiceBusProcessor(client, topicName, subscriptionName, options);

        processor.EntityPath.Should().Be($"{topicName}/subscriptions/{subscriptionName}");
        processor.MaxConcurrentCalls.Should().Be(3);
        processor.IsProcessing.Should().BeFalse();
        processor.IsClosed.Should().BeFalse();
    }

    #endregion

    #region Property Tests
    [TestMethod]
    public async Task ProcessorOptions_AreSetCorrectly()
    {

        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue");
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);

        var options = new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 5,
            AutoCompleteMessages = false,
            PrefetchCount = 10,
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        };

        await using var processor = client.CreateProcessor("test-queue", options);

        processor.MaxConcurrentCalls.Should().Be(5);
        processor.AutoCompleteMessages.Should().BeFalse();
        processor.PrefetchCount.Should().Be(10);
        processor.ReceiveMode.Should().Be(ServiceBusReceiveMode.ReceiveAndDelete);
        processor.EntityPath.Should().Be("test-queue");
        processor.FullyQualifiedNamespace.Should().Be(client.FullyQualifiedNamespace);
    }

    #endregion

    #region Lifecycle Tests

    [TestMethod]
    public async Task StartProcessingAsync_SetsIsProcessingToTrue()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue");
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateProcessor("test-queue");
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
        await using var processor = client.CreateProcessor("test-queue");
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
        await using var processor = client.CreateProcessor("test-queue");
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
        await using var processor = client.CreateProcessor("test-queue");

        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task CloseAsync_SetsIsClosedToTrue()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateProcessor("test-queue");

        await processor.CloseAsync();
        processor.IsClosed.Should().BeTrue();

    }

    [TestMethod]
    public async Task DisposeAsync_SetsIsClosedToTrue()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateProcessor("test-queue");

        await processor.DisposeAsync();
        processor.IsClosed.Should().BeTrue();

    }

    [TestMethod]
    public async Task CloseAsync_WhenProcessing_StopProcessingFirst()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateProcessor("test-queue");
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
        var processor = client.CreateProcessor("test-queue");

        processor.ProcessMessageAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await processor.CloseAsync();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
            () => processor.StartProcessingAsync());
    }
    #endregion

    #region MessageProcessing Tests

    [TestMethod]
    public async Task ProcessMessage_ReceivesAndProcessMessage()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue");
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateProcessor("test-queue");

        var messageReceived = new TaskCompletionSource<ServiceBusReceivedMessage>();
        processor.ProcessMessageAsync += args =>
        {
            messageReceived.TrySetResult(args.Message);
            return Task.CompletedTask;
        };
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await using var sender = client.CreateSender("test-queue");
        await sender.SendMessageAsync(new ServiceBusMessage("Test Message"));

        await processor.StartProcessingAsync();
        var receivedMessage = await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        receivedMessage.Body.ToString().Should().Be("Test Message");

        await processor.StopProcessingAsync();

    }

    [TestMethod]
    public async Task ProcessMessage_WithAutoComplete_CompletesMessageAutomatically()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue");
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateProcessor("test-queue", new ServiceBusProcessorOptions()
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
        await sender.SendMessageAsync(new ServiceBusMessage("Test Message"));

        await processor.StartProcessingAsync();
        await messageProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var isCompleted = await WaitForMessageCompletion(client, "test-queue", TimeSpan.FromSeconds(2));
        isCompleted.Should().BeTrue("message should be auto-completed and queue should be empty");

        await processor.StopProcessingAsync();
    }


    [TestMethod]
    public async Task ProcessMessage_WithManualComplete_DoesNotAutoComplete()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue");
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateProcessor("test-queue", new ServiceBusProcessorOptions
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
        await sender.SendMessageAsync(new ServiceBusMessage("Test Message"));

        await processor.StartProcessingAsync();
        await messageProcessed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var isCompleted = await WaitForMessageCompletion(client, "test-queue", TimeSpan.FromSeconds(2));
        isCompleted.Should().BeTrue("message should be manually completed and queue should be empty");

        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task ProcessMessage_WithException_CallsErrorHandler()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue");
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateProcessor("test-queue");

        var errorHandled = new TaskCompletionSource<ProcessErrorEventArgs>();
        processor.ProcessMessageAsync += _ => throw new InvalidOperationException("Test exception");
        processor.ProcessErrorAsync += args =>
        {
            errorHandled.TrySetResult(args);
            return Task.CompletedTask;
        };

        await using var sender = client.CreateSender("test-queue");
        await sender.SendMessageAsync(new ServiceBusMessage("Test Message"));

        await processor.StartProcessingAsync();
        var errorArgs = await errorHandled.Task.WaitAsync(TimeSpan.FromSeconds(5));

        errorArgs.Exception.Should().BeOfType<InvalidOperationException>();
        errorArgs.Exception.Message.Should().Be("Test exception");
        errorArgs.ErrorSource.Should().Be(ServiceBusErrorSource.Receive);
        errorArgs.EntityPath.Should().Be("test-queue");

        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task ProcessMessage_WithConcurrency_ProcessesMultipleMessagesSimultaneously()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue");
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateProcessor("test-queue", new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 3
        });

        var processedMessages = new ConcurrentBag<string>();
        var processingStarted = new TaskCompletionSource<bool>();
        var continueProcessing = new TaskCompletionSource<bool>();
        var messageCount = 0;

        processor.ProcessMessageAsync += async args =>
        {
            processedMessages.Add(args.Message.Body.ToString());

            if (Interlocked.Increment(ref messageCount) == 1)
            {
                processingStarted.TrySetResult(true);
            }
            await continueProcessing.Task;
        };
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await using var sender = client.CreateSender("test-queue");
        for (var i = 0; i < 3; i++)
        {
            await sender.SendMessageAsync(new ServiceBusMessage($"Message {i}"));
        }

        await processor.StartProcessingAsync();
        await processingStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        continueProcessing.TrySetResult(true);

        await Task.Delay(1000);

        processedMessages.Should().HaveCount(3);
        processedMessages.Should().Contain("Message 0");
        processedMessages.Should().Contain("Message 1");
        processedMessages.Should().Contain("Message 2");

        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task ProcessMessage_FromSubscription_ReceivesTopicMessage()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        var subscription = ns.AddTopic("test-topic").AddSubscription("test-subscription");

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateProcessor(subscription.TopicName, subscription.SubscriptionName);

        var messageReceived = new TaskCompletionSource<ServiceBusReceivedMessage>();
        processor.ProcessMessageAsync += args =>
        {
            messageReceived.TrySetResult(args.Message);
            return Task.CompletedTask;
        };
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await using var sender = client.CreateSender("test-topic");
        await sender.SendMessageAsync(new ServiceBusMessage("Topic message"));

        await processor.StartProcessingAsync();
        var receivedMessage = await messageReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));

        receivedMessage.Body.ToString().Should().Be("Topic message");

        await processor.StopProcessingAsync();
    }

    #endregion

    #region Handlers
    [TestMethod]
    public async Task ProcessMessageAsync_AddSecondHandler_ThrowsNotSupportedException()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue");
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateProcessor("test-queue");

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
        ns.AddQueue("test-queue");
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateProcessor("test-queue");

        Func<ProcessMessageEventArgs, Task> handler1 = _ => Task.CompletedTask;
        Func<ProcessMessageEventArgs, Task> handler2 = _ => Task.CompletedTask;

        processor.ProcessMessageAsync += handler1;
        processor.ProcessMessageAsync -= handler1;
        processor.ProcessMessageAsync += handler2;
    }
    #endregion

    #region HelperMethods
    private static async Task<bool> WaitForMessageCompletion(InMemoryServiceBusClient client, string queueName, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < deadline)
        {
            await using var receiver = client.CreateReceiver(queueName);
            var messages = await receiver.ReceiveMessagesAsync(1, TimeSpan.FromMilliseconds(100));

            if (messages.Count == 0)
            {
                return true;
            }
            await Task.Delay(50);
        }
        return false;
    }
    #endregion

    [TestMethod]
    public async Task ConcurrentStartStop_DoesNotCauseDeadlock()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue");
        var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateProcessor("test-queue");

        processor.ProcessMessageAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        try
        {
            var random = new Random();

            for (var i = 0; i < 5; i++)
            {
                await processor.StartProcessingAsync();
                await Task.Delay(random.Next(10, 50));
                await processor.StopProcessingAsync();
                await Task.Delay(random.Next(10, 50));
            }

            await processor.StartProcessingAsync();

            var concurrentStartTasks = new List<Task>();
            for (var i = 0; i < 3; i++)
            {
                concurrentStartTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await processor.StartProcessingAsync();
                    }
                    catch (InvalidOperationException)
                    {
                        // Expected - already processing
                    }
                }));
            }

            await Task.WhenAll(concurrentStartTasks);
            await processor.StopProcessingAsync();

            processor.IsProcessing.Should().BeFalse();
        }
        finally
        {
            await processor.CloseAsync();
            await client.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task StopProcessing_WaitsForInFlightMessages()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue");
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateProcessor("test-queue");

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
        await sender.SendMessageAsync(new ServiceBusMessage("Test"));

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
        ns.AddQueue("test-queue");
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateProcessor("test-queue");

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

