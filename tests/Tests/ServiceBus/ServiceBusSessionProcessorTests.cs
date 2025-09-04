using System.Collections.Concurrent;

using Azure.Messaging.ServiceBus;

using Spotflow.InMemory.Azure.ServiceBus;
using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Tests.ServiceBus;

[TestClass]
public class ServiceBusSessionProcessorTests
{

    #region Handlers Tests
    [TestMethod]
    public async Task Cannot_AddNullHandler()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new InMemoryServiceBusQueueOptions
        {
            EnableSessions = true
        });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);

        var processor = client.CreateSessionProcessor("test-queue");
        var assignNullProcessMessageAsync = () => processor.ProcessMessageAsync += null;
        var assignNullProcessErrorAsync = () => processor.ProcessErrorAsync += null;
        var assignNullSessionInitializingAsync = () => processor.SessionInitializingAsync += null;
        var assignNullSessionClosingAsync = () => processor.SessionClosingAsync += null;
        assignNullProcessMessageAsync.Should().Throw<ArgumentNullException>();
        assignNullProcessErrorAsync.Should().Throw<ArgumentNullException>();
        assignNullSessionInitializingAsync.Should().Throw<ArgumentNullException>();
        assignNullSessionClosingAsync.Should().Throw<ArgumentNullException>();
    }

    [TestMethod]
    public async Task CannotAdd_TwoHandlersToTheSameEvent()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new InMemoryServiceBusQueueOptions
        {
            EnableSessions = true
        });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);

        var processor = client.CreateSessionProcessor("test-queue");
        processor.ProcessMessageAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;
        processor.SessionInitializingAsync += _ => Task.CompletedTask;
        processor.SessionClosingAsync += _ => Task.CompletedTask;

        var assignProcessMessage = () => processor.ProcessMessageAsync += _ => Task.CompletedTask;
        var assignProcessError = () => processor.ProcessErrorAsync += _ => Task.CompletedTask;
        var assignSessionInit = () => processor.SessionInitializingAsync += _ => Task.CompletedTask;
        var assignSessionClose = () => processor.SessionClosingAsync += _ => Task.CompletedTask;

        assignProcessMessage.Should().Throw<NotSupportedException>();
        assignProcessError.Should().Throw<NotSupportedException>();
        assignSessionInit.Should().Throw<NotSupportedException>();
        assignSessionClose.Should().Throw<NotSupportedException>();
    }
    #endregion

    #region Initialization Tests
    [TestMethod]
    public async Task Constructor_WithQueue_SetsPropertiesCorrectly()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue("test-queue", new InMemoryServiceBusQueueOptions
        {
            EnableSessions = true
        });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var options = new ServiceBusSessionProcessorOptions
        {
            MaxConcurrentSessions = 5,
            MaxConcurrentCallsPerSession = 2,
            AutoCompleteMessages = false,
        };
        await using var processor = client.CreateSessionProcessor("test-queue", options);
        processor.EntityPath.Should().Be("test-queue");
        processor.MaxConcurrentSessions.Should().Be(5);
        processor.MaxConcurrentCallsPerSession.Should().Be(2);
        processor.AutoCompleteMessages.Should().BeFalse();
        processor.IsProcessing.Should().BeFalse();
        processor.IsClosed.Should().BeFalse();
        processor.FullyQualifiedNamespace.Should().Be(client.FullyQualifiedNamespace);
    }

    [TestMethod]
    public async Task Constructor_WithSubscription_SetsPropertiesCorrectly()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        const string testTopic = "test-topic";
        const string testSubscription = "test-subscription";
        ns.AddTopic(testTopic).AddSubscription(testSubscription, new InMemoryServiceBusSubscriptionOptions
        {
            EnableSessions = true
        });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var options = new ServiceBusSessionProcessorOptions()
        {
            MaxConcurrentSessions = 3,
            MaxConcurrentCallsPerSession = 4
        };
        await using var processor = client.CreateSessionProcessor(testTopic, testSubscription, options);

        processor.EntityPath.Should().Be($"{testTopic}/subscriptions/{testSubscription}");
        processor.MaxConcurrentSessions.Should().Be(3);
        processor.MaxConcurrentCallsPerSession.Should().Be(4);
        processor.IsProcessing.Should().BeFalse();
        processor.IsClosed.Should().BeFalse();
    }
    #endregion

    #region Lifecycle Tests

    [TestMethod]
    public async Task StartProcessingAsync_SetsIsProcessingToTrue()
    {
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        const string testQueue = "test-queue";
        ns.AddQueue(testQueue, new InMemoryServiceBusQueueOptions
        {
            EnableSessions = true
        });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor(testQueue);
        processor.SessionInitializingAsync += _ => Task.CompletedTask;
        processor.ProcessMessageAsync += _ => Task.CompletedTask;
        processor.SessionClosingAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await processor.StartProcessingAsync();
        processor.IsProcessing.Should().BeTrue();
        await processor.StopProcessingAsync();
    }

    [TestMethod]
    public async Task StartProcessingAsync_WhenAlreadyProcessing_ThrowsInvalidOperationException()
    {
        const string testQueue = "test-queue";
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue(testQueue, new InMemoryServiceBusQueueOptions { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor(testQueue);
        processor.SessionInitializingAsync += _ => Task.CompletedTask;
        processor.ProcessMessageAsync += _ => Task.CompletedTask;
        processor.SessionClosingAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await processor.StartProcessingAsync();

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => processor.StartProcessingAsync());
        await processor.StopProcessingAsync();

    }

    [TestMethod]
    public async Task StartProcessingAsync_WhenClosed_ThrowsObjectDisposedException()
    {
        const string testQueue = "test-queue";
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue(testQueue, new InMemoryServiceBusQueueOptions { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var processor = client.CreateSessionProcessor(testQueue);
        await processor.CloseAsync();
        await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() => processor.StartProcessingAsync());
    }

    [TestMethod]
    public async Task StopProcessingAsync_SetsIsProcessingToFalse()
    {
        const string testQueue = "test-queue";
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue(testQueue, new InMemoryServiceBusQueueOptions { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor(testQueue);
        processor.SessionInitializingAsync += _ => Task.CompletedTask;
        processor.ProcessMessageAsync += _ => Task.CompletedTask;
        processor.SessionClosingAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += _ => Task.CompletedTask;

        await processor.StartProcessingAsync();
        await processor.StopProcessingAsync();

        processor.IsProcessing.Should().BeFalse();
    }

    #endregion

    #region Session Processing Tests

    [TestMethod]
    public async Task ProcessSessionMessage_WithException_CallsErrorHandler()
    {
        const string testQueue = "test-queue";
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue(testQueue, new InMemoryServiceBusQueueOptions { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor(testQueue);

        var errorHandled = false;
        var expectedException = new InvalidOperationException("Test exception");

        processor.SessionInitializingAsync += _ => Task.CompletedTask;
        processor.SessionClosingAsync += _ => Task.CompletedTask;

        processor.ProcessMessageAsync += _ => throw expectedException;

        processor.ProcessErrorAsync += args =>
        {
            errorHandled = true;
            args.Exception.Should().BeOfType<InvalidOperationException>();
            args.Exception.Message.Should().Be(expectedException.Message);
            return Task.CompletedTask;
        };


        await using var sender = client.CreateSender(testQueue);
        var message = new ServiceBusMessage(BinaryData.FromString("test message")) { SessionId = "session1" };
        await sender.SendMessageAsync(message);
        await processor.StartProcessingAsync();
        await Task.Delay(1000);
        await processor.StopProcessingAsync();

        errorHandled.Should().BeTrue();

    }

    [TestMethod]
    public async Task ProcessSessionMessage_WithAutoCompleteEnabled_ProcessesMessages()
    {
        const string testQueue = "test-queue";
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue(testQueue, new InMemoryServiceBusQueueOptions { EnableSessions = true });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var options = new ServiceBusSessionProcessorOptions { AutoCompleteMessages = true };
        await using var processor = client.CreateSessionProcessor(testQueue, options);

        var messageProcessed = false;
        var sessionInitialized = false;
        var sessionClosed = false;
        processor.SessionInitializingAsync += _ =>
        {
            sessionInitialized = true;
            return Task.CompletedTask;
        };

        processor.ProcessMessageAsync += _ =>
        {
            messageProcessed = true;
            return Task.CompletedTask;
        };

        processor.SessionClosingAsync += _ =>
        {
            sessionClosed = true;
            return Task.CompletedTask;
        };

        processor.ProcessErrorAsync += args =>
        {
            Assert.Fail($"Unexpected error: {args.Exception}");
            return Task.CompletedTask;
        };
        await processor.StartProcessingAsync();

        await using var sender = client.CreateSender(testQueue);
        var message = new ServiceBusMessage(BinaryData.FromString("test message")) { SessionId = "session1" };
        await sender.SendMessageAsync(message);

        await Task.Delay(1000);
        await processor.StopProcessingAsync();

        messageProcessed.Should().BeTrue();
        sessionInitialized.Should().BeTrue();
        sessionClosed.Should().BeTrue();
    }

    [TestMethod]
    public async Task ProcessSessionMessage_WithSpecificSessionIds_ProcessOnlySpecifiedSessions()
    {
        const string testQueue = "test-queue";
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue(testQueue, new InMemoryServiceBusQueueOptions
        {
            EnableSessions = true
        });
        var clientOptions = new ServiceBusClientOptions
        {
            RetryOptions =
            {
                TryTimeout = TimeSpan.FromSeconds(2),
            }
        };

        await using var client = InMemoryServiceBusClient.FromNamespace(ns, clientOptions);
        var targetSessionIds = new[] { "session1", "session3" };
        var options = new ServiceBusSessionProcessorOptions
        {
            SessionIds =
            {
                "session1",
                "session3"
            },
            AutoCompleteMessages = true
        };

        await using var processor = client.CreateSessionProcessor(testQueue, options);
        var allMessagesProcessed = new TaskCompletionSource<bool>();
        var processedMessages = new ConcurrentBag<(string SessionId, string Body)>();
        var processedSessions = new ConcurrentBag<string>();
        const int expectedMessageCount = 3;
        var processedCount = 0;

        processor.SessionInitializingAsync += args =>
        {
            processedSessions.Add(args.SessionId);
            return Task.CompletedTask;
        };

        processor.ProcessMessageAsync += args =>
        {
            processedMessages.Add((args.SessionId, args.Message.Body.ToString()));
            if (Interlocked.Increment(ref processedCount) >= expectedMessageCount)
            {
                allMessagesProcessed.TrySetResult(true);
            }
            return Task.CompletedTask;
        };

        processor.SessionClosingAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += args =>
        {
            Assert.Fail($"Unexpected error: {args.Exception}");
            return Task.CompletedTask;
        };
        await processor.StartProcessingAsync();
        await using var sender = client.CreateSender(testQueue);

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Message for session1"))
        {
            SessionId = "session1"
        });

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Message for session2"))
        {
            SessionId = "session2"
        });
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Message for session3"))
        {
            SessionId = "session3"
        });
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Another message for session1"))
        {
            SessionId = "session1"
        });


        var completionTask = allMessagesProcessed.Task;
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));
        var completedTask = await Task.WhenAny(completionTask, timeoutTask);
        if (completedTask == timeoutTask)
        {
            Assert.Fail($"Test timed out. Processed {processedCount} out of {expectedMessageCount} messages");
        }
        await processor.StopProcessingAsync();
        var messagesList = processedMessages.ToList();
        var sessionsList = processedSessions.ToHashSet();

        messagesList.Should().HaveCount(3);
        messagesList.Should().Contain(m => m.SessionId == "session1" && m.Body == "Message for session1");
        messagesList.Should().Contain(m => m.SessionId == "session1" && m.Body == "Another message for session1");
        messagesList.Should().Contain(m => m.SessionId == "session3" && m.Body == "Message for session3");
        messagesList.Should().NotContain(m => m.SessionId == "session2");

        sessionsList.Should().BeEquivalentTo(targetSessionIds);
    }

    [TestMethod]
    public async Task ProcessSessionMessage_WithMultipleConcurrentSessions_ProcessesCorrectly()
    {
        const string testQueue = "test-queue";
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue(testQueue, new InMemoryServiceBusQueueOptions
        {
            EnableSessions = true
        });

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var options = new ServiceBusSessionProcessorOptions
        {
            MaxConcurrentSessions = 4,
            MaxConcurrentCallsPerSession = 2,
            AutoCompleteMessages = true
        };

        await using var processor = client.CreateSessionProcessor(testQueue, options);

        var processedMessages = new ConcurrentBag<(string SessionId, string Body, DateTime ProcessedAt)>();
        var sessionInitializations = new ConcurrentBag<string>();
        var sessionClosures = new ConcurrentBag<string>();
        var allMessagesProcessed = new TaskCompletionSource<bool>();
        const int expectedMessageCount = 12; // 4 sessions * 3 messages each
        var processedCount = 0;

        processor.SessionInitializingAsync += args =>
        {
            sessionInitializations.Add(args.SessionId);
            return Task.CompletedTask;
        };
        processor.ProcessMessageAsync += async args =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            processedMessages.Add((args.SessionId, args.Message.Body.ToString(), DateTime.UtcNow));

            if (Interlocked.Increment(ref processedCount) >= expectedMessageCount)
            {
                allMessagesProcessed.TrySetResult(true);
            }
        };

        processor.SessionClosingAsync += args =>
        {
            sessionClosures.Add(args.SessionId);
            return Task.CompletedTask;
        };

        processor.ProcessErrorAsync += args =>
        {
            allMessagesProcessed.TrySetException(args.Exception);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync();
        await using var sender = client.CreateSender(testQueue);
        var sessions = new[] { "sessionA", "sessionB", "sessionC", "sessionD" };
        foreach (var sessionId in sessions)
        {
            for (var i = 1; i <= 3; i++)
            {
                await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString($"Message {i} for {sessionId}")) { SessionId = sessionId });
            }
        }

        var completionTask = allMessagesProcessed.Task;
        var timeoutTask = Task.Delay(TimeSpan.FromMinutes(1));
        var completedTask = await Task.WhenAny(completionTask, timeoutTask);
        if (completedTask == timeoutTask)
        {
            Assert.Fail($"Test timed out. Processed {processedCount} out of {expectedMessageCount}");
        }

        await processor.StopProcessingAsync();
        var messagesList = processedMessages.ToList();
        messagesList.Should().HaveCount(12);
        foreach (var sessionId in sessions)
        {
            var sessionMessages = messagesList.Where(x => x.SessionId == sessionId).ToList();
            sessionMessages.Should().HaveCount(3);
            sessionMessages.Should().Contain(m => m.Body == $"Message 1 for {sessionId}");
            sessionMessages.Should().Contain(m => m.Body == $"Message 2 for {sessionId}");
            sessionMessages.Should().Contain(m => m.Body == $"Message 3 for {sessionId}");
        }

        sessionInitializations.ToHashSet().Should().BeEquivalentTo(sessions);
        sessionClosures.ToHashSet().Should().BeEquivalentTo(sessions);
    }

    [TestMethod]
    public async Task ProcessSessionMessage_WithSessionIdleTimeout_ClosesIdleSessions()
    {
        const string testQueue = "test-queue";
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue(testQueue, new InMemoryServiceBusQueueOptions
        {
            EnableSessions = true
        });

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        var options = new ServiceBusSessionProcessorOptions()
        {
            SessionIdleTimeout = TimeSpan.FromMilliseconds(500),
            AutoCompleteMessages = true
        };
        await using var processor = client.CreateSessionProcessor(testQueue, options);


        var processedMessages = new ConcurrentBag<string>();
        var sessionsClosed = new ConcurrentBag<string>();
        var allMessagesProcessed = new TaskCompletionSource<bool>();
        const int expectedMessageCount = 2;
        var processedCount = 0;

        processor.SessionInitializingAsync += _ => Task.CompletedTask;
        processor.ProcessMessageAsync += args =>
        {
            processedMessages.Add(args.Message.Body.ToString());
            if (Interlocked.Increment(ref processedCount) >= expectedMessageCount)
            {
                allMessagesProcessed.TrySetResult(true);
            }

            return Task.CompletedTask;
        };
        processor.SessionClosingAsync += args =>
        {
            sessionsClosed.Add(args.SessionId);
            return Task.CompletedTask;
        };
        processor.ProcessErrorAsync += args =>
        {
            allMessagesProcessed.TrySetException(args.Exception);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync();
        await using var sender = client.CreateSender(testQueue);

        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Initial Message"))
        {
            SessionId = "session1"
        });

        await Task.Delay(300);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Second Message"))
        {
            SessionId = "session1"
        });
        var completionTask = allMessagesProcessed.Task;
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var completedTask = await Task.WhenAny(completionTask, timeoutTask);
        if (completedTask == timeoutTask)
        {
            Assert.Fail($"Test timed out. Processed {processedCount} out of {expectedMessageCount} messages");
        }

        await Task.Delay(1000);
        await processor.StopProcessingAsync();
        var messagesList = processedMessages.ToList();
        messagesList.Should().HaveCount(2);
        messagesList.Should().Contain("Initial Message");
        messagesList.Should().Contain("Second Message");
        sessionsClosed.Should().Contain("session1");
    }

    [TestMethod]
    public async Task ProcessSessionMessage_WithTopicSubscription_ProcessesCorrectly()
    {
        const string topicName = "test-topic";
        const string subscriptionName = "test-subscription";
        var ns = new InMemoryServiceBusProvider().AddNamespace();
        var topic = ns.AddTopic(topicName);
        topic.AddSubscription(subscriptionName, new InMemoryServiceBusSubscriptionOptions()
        {
            EnableSessions = true
        });
        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var processor = client.CreateSessionProcessor(topicName, subscriptionName);

        var messageProcessed = new TaskCompletionSource<(string SessionId, string Body)>();
        var sessionInitialized = new TaskCompletionSource<string>();

        processor.SessionInitializingAsync += args =>
        {
            sessionInitialized.TrySetResult(args.SessionId);
            return Task.CompletedTask;
        };

        processor.ProcessMessageAsync += args =>
        {
            messageProcessed.TrySetResult((args.SessionId, args.Message.Body.ToString()));
            return Task.CompletedTask;
        };

        processor.SessionClosingAsync += _ => Task.CompletedTask;
        processor.ProcessErrorAsync += args =>
        {
            messageProcessed.TrySetException(args.Exception);
            sessionInitialized.TrySetException(args.Exception);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync();
        await using var sender = client.CreateSender(topicName);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromString("Topic message"))
        {
            SessionId = "topic-session1"
        });
        var messageTask = messageProcessed.Task;
        var sessionTask = sessionInitialized.Task;
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

        var messageCompleted = await Task.WhenAny(messageTask, timeoutTask);
        var sessionCompleted = await Task.WhenAny(sessionTask, timeoutTask);

        if (messageCompleted == timeoutTask || sessionCompleted == timeoutTask)
        {
            Assert.Fail("Test timed out.");
        }
        await processor.StopProcessingAsync();
        var (sessionId, body) = await messageTask;
        var initializedSessionId = await sessionTask;

        sessionId.Should().Be("topic-session1");
        body.Should().Be("Topic message");
        initializedSessionId.Should().Be("topic-session1");
    }

    [TestMethod]
    public async Task ProcessSessionMessage_WithReplyToSessionId_SendsReplyToCorrectSession()
    {
        const string requestQueue = "test-request-queue";
        const string replyQueue = "test-reply-queue";

        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue(requestQueue, new InMemoryServiceBusQueueOptions()
        {
            EnableSessions = true
        });
        ns.AddQueue(replyQueue, new InMemoryServiceBusQueueOptions()
        {
            EnableSessions = true
        });

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var requestProcessor = client.CreateSessionProcessor(requestQueue);
        await using var replyProcessor = client.CreateSessionProcessor(replyQueue);
        await using var requestSender = client.CreateSender(requestQueue);
        await using var replySender = client.CreateSender(replyQueue);

        var requestProcessed = new TaskCompletionSource<ServiceBusReceivedMessage>();
        var replyReceived = new TaskCompletionSource<ServiceBusReceivedMessage>();

        requestProcessor.SessionInitializingAsync += _ => Task.CompletedTask;
        requestProcessor.SessionClosingAsync += _ => Task.CompletedTask;
        const string messageId = "msg-123";
        requestProcessor.ProcessMessageAsync += async args =>
        {
            requestProcessed.TrySetResult(args.Message);

            const string replyContent = "test-reply-123";
            var replyMessage = new ServiceBusMessage(BinaryData.FromString(replyContent))
            {
                MessageId = messageId,
                CorrelationId = args.Message.MessageId,
                SessionId = args.Message.ReplyToSessionId
            };
            await replySender.SendMessageAsync(replyMessage);
        };

        requestProcessor.ProcessErrorAsync += args =>
        {
            requestProcessed.TrySetException(args.Exception);
            return Task.CompletedTask;
        };

        replyProcessor.SessionInitializingAsync += _ => Task.CompletedTask;
        replyProcessor.SessionClosingAsync += _ => Task.CompletedTask;

        replyProcessor.ProcessMessageAsync += args =>
        {
            replyReceived.TrySetResult(args.Message);
            return Task.CompletedTask;
        };

        replyProcessor.ProcessErrorAsync += args =>
        {
            replyReceived.TrySetException(args.Exception);
            return Task.CompletedTask;
        };

        await requestProcessor.StartProcessingAsync();
        await replyProcessor.StartProcessingAsync();

        var requestMessage = new ServiceBusMessage(BinaryData.FromString("test-request-123"))
        {
            MessageId = "request-msg-1",
            SessionId = "processing-session",
            ReplyToSessionId = "client-session-123"
        };

        await requestSender.SendMessageAsync(requestMessage);

        var requestTask = requestProcessed.Task;
        var replyTask = replyReceived.Task;
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));

        var requestCompleted = await Task.WhenAny(requestTask, timeoutTask);
        if (requestCompleted == timeoutTask)
        {
            Assert.Fail("Test timed out waiting for request to be processed");
        }
        var replyCompleted = await Task.WhenAny(replyTask, timeoutTask);
        if (replyCompleted == timeoutTask)
        {
            Assert.Fail("Test timed out waiting for reply to be received");
        }

        await requestProcessor.StopProcessingAsync();
        await replyProcessor.StopProcessingAsync();

        var processedRequest = await requestTask;
        processedRequest.MessageId.Should().Be("request-msg-1");
        processedRequest.SessionId.Should().Be("processing-session");
        processedRequest.ReplyToSessionId.Should().Be("client-session-123");

        var receivedReply = await replyTask;
        receivedReply.SessionId.Should().Be("client-session-123");
        receivedReply.CorrelationId.Should().Be("request-msg-1");
        receivedReply.MessageId.Should().Be(messageId);
        receivedReply.Body.ToString().Should().Be("test-reply-123");

    }

    [TestMethod]
    public async Task ProcessSessionMessage_WithMultipleReplyToSessions_RoutesCorrectly()
    {
        const string requestQueue = "test-request-queue";
        const string replyQueue = "test-reply-queue";

        var ns = new InMemoryServiceBusProvider().AddNamespace();
        ns.AddQueue(requestQueue, new InMemoryServiceBusQueueOptions
        {
            EnableSessions = true
        });
        ns.AddQueue(replyQueue, new InMemoryServiceBusQueueOptions
        {
            EnableSessions = true
        });

        await using var client = InMemoryServiceBusClient.FromNamespace(ns);
        await using var requestProcessor = client.CreateSessionProcessor(requestQueue);
        await using var replyProcessor = client.CreateSessionProcessor(replyQueue);
        await using var requestSender = client.CreateSender(requestQueue);
        await using var replySender = client.CreateSender(replyQueue);

        var repliesReceived = new ConcurrentBag<(string SessionId, string CorrelationId, string MessageId, string Body)>();
        var allRepliesReceived = new TaskCompletionSource<bool>();
        const int expectedReplies = 4;
        var replyCount = 0;

        requestProcessor.SessionInitializingAsync += _ => Task.CompletedTask;
        requestProcessor.SessionClosingAsync += _ => Task.CompletedTask;
        requestProcessor.ProcessMessageAsync += async args =>
        {
            var replyContent = $"Reply to {args.Message.MessageId}";
            var replyMessage = new ServiceBusMessage(BinaryData.FromString(replyContent))
            {
                MessageId = $"reply-{args.Message.MessageId}",
                CorrelationId = args.Message.MessageId,
                SessionId = args.Message.ReplyToSessionId
            };
            await replySender.SendMessageAsync(replyMessage);
        };

        requestProcessor.ProcessErrorAsync += args =>
        {
            allRepliesReceived.TrySetException(args.Exception);
            return Task.CompletedTask;
        };

        replyProcessor.SessionInitializingAsync += _ => Task.CompletedTask;
        replyProcessor.SessionClosingAsync += _ => Task.CompletedTask;

        replyProcessor.ProcessMessageAsync += args =>
        {
            repliesReceived.Add((args.SessionId, args.Message.CorrelationId, args.Message.MessageId, args.Message.Body.ToString()));
            if (Interlocked.Increment(ref replyCount) == expectedReplies)
            {
                allRepliesReceived.TrySetResult(true);
            }

            return Task.CompletedTask;
        };

        replyProcessor.ProcessErrorAsync += args =>
        {
            allRepliesReceived.TrySetException(args.Exception);
            return Task.CompletedTask;
        };

        await requestProcessor.StartProcessingAsync();
        await replyProcessor.StartProcessingAsync();

        var requests = new[]
        {
            new
            {
                MessageId = "req-1",
                ProcessingSession = "worker-1",
                ReplySession = "client-alpha"
            },
            new
            {
                MessageId = "req-2",
                ProcessingSession = "worker-2",
                ReplySession = "client-beta"
            },
            new
            {
                MessageId = "req-3",
                ProcessingSession = "worker-1",
                ReplySession = "client-gamma"
            },
            new
            {
                MessageId = "req-4",
                ProcessingSession = "worker-3",
                ReplySession = "client-alpha"
            } // same reply session as req-1
        };

        foreach (var req in requests)
        {
            var message = new ServiceBusMessage(BinaryData.FromString($"Request {req.MessageId}"))
            {
                MessageId = req.MessageId,
                SessionId = req.ProcessingSession,
                ReplyToSessionId = req.ReplySession
            };
            await requestSender.SendMessageAsync(message);
        }
        var completionTask = allRepliesReceived.Task;
        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(15));
        var completedTask = await Task.WhenAny(completionTask, timeoutTask);
        if (completedTask == timeoutTask)
        {
            Assert.Fail($"Test timed out. Received {replyCount} out of {expectedReplies} replies");
        }

        await requestProcessor.StopProcessingAsync();
        await replyProcessor.StopProcessingAsync();

        var repliesList = repliesReceived.ToList();
        repliesList.Should().HaveCount(4);
        repliesList.Should().Contain(r => r.SessionId == "client-alpha" && r.CorrelationId == "req-1" && r.Body == "Reply to req-1");
        repliesList.Should().Contain(r => r.SessionId == "client-beta" && r.CorrelationId == "req-2" && r.Body == "Reply to req-2");
        repliesList.Should().Contain(r => r.SessionId == "client-gamma" && r.CorrelationId == "req-3" && r.Body == "Reply to req-3");
        repliesList.Should().Contain(r => r.SessionId == "client-alpha" && r.CorrelationId == "req-4" && r.Body == "Reply to req-4");

        var alphaReplies = repliesList.Where(r => r.SessionId == "client-alpha").ToList();
        alphaReplies.Should().HaveCount(2);
    }

    #endregion
}
