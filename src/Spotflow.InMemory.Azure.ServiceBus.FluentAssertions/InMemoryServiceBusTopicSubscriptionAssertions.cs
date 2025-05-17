using Azure.Messaging.ServiceBus;

using FluentAssertions.Primitives;

using Spotflow.InMemory.Azure.ServiceBus.FluentAssertions.Internal;
using Spotflow.InMemory.Azure.ServiceBus.Resources;

namespace Spotflow.InMemory.Azure.ServiceBus.FluentAssertions;

public class InMemoryServiceBusTopicSubscriptionAssertions(InMemoryServiceBusSubscription subject)
    : ReferenceTypeAssertions<InMemoryServiceBusSubscription, InMemoryServiceBusTopicSubscriptionAssertions>(subject)
{
    protected override string Identifier => nameof(InMemoryServiceBusSubscription);

    public async Task BeEmptyAsync(TimeSpan? maxWaitTime = null, string? because = null, params object[] becauseArgs)
    {
        var entity = $"{Subject.Topic.TopicName}/{Subject.SubscriptionName}";

        await ServiceBusAssertionHelpers.EntityShouldBeEmptyAsync(entity, () => Subject.MessageCount, maxWaitTime, because, becauseArgs);
    }

    /// <summary>
    /// Accepts a session and receives a message from the session.
    /// </summary>
    /// <param name="sessionId">Session ID to accept. If null, the next available session will be accepted.</param>
    /// <param name="maxWaitTime">Maximum wait time for receiving the message. Default is 8 seconds.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    public async Task<ServiceBusReceivedMessage> ReceiveMessageFromSessionAsync(
        string? sessionId = null,
        TimeSpan? maxWaitTime = null,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        maxWaitTime ??= TimeSpan.FromSeconds(8);

        using var timeCts = new CancellationTokenSource(maxWaitTime.Value, timeProvider ?? TimeProvider.System);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeCts.Token);

        var (topicName, subscriptionName) = (Subject.TopicName, Subject.SubscriptionName);

        await using var receiverClient = InMemoryServiceBusClient.FromNamespace(Subject.Namespace);

        try
        {
            ServiceBusSessionReceiver session;

            if (sessionId is null)
            {
                session = await receiverClient.AcceptNextSessionAsync(topicName, subscriptionName, cancellationToken: cts.Token);
            }
            else
            {
                session = await receiverClient.AcceptSessionAsync(topicName, subscriptionName, sessionId, cancellationToken: cts.Token);
            }

            await using (session)
            {
                return await session.ReceiveMessageAsync(cancellationToken: cts.Token);
            }
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            throw new OperationCanceledException("No session message received soon enough.", ex, cts.Token);
        }
    }
}
