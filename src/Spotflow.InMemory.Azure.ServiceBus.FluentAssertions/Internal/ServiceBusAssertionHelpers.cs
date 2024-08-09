using System.Diagnostics;

using FluentAssertions.Execution;

namespace Spotflow.InMemory.Azure.ServiceBus.FluentAssertions.Internal;

internal static class ServiceBusAssertionHelpers
{
    public static async Task EntityShouldBeEmptyAsync(string entity, Func<long> count, TimeSpan? maxWaitTime = null, string? because = null, params object[] becauseArgs)
    {
        maxWaitTime ??= TimeSpan.FromSeconds(8);

        var startTime = Stopwatch.GetTimestamp();

        long? lastCount = null;

        while (Stopwatch.GetElapsedTime(startTime) < maxWaitTime)
        {
            lastCount = count();

            if (lastCount == 0)
            {
                return;
            }

            await Task.Delay(10);
        }

        Execute
            .Assertion
            .BecauseOf(because, becauseArgs)
            .FailWith(
                "Entity {0} should be empty{reason} but {1} messages found after {2} seconds.",
                entity,
                lastCount,
                maxWaitTime.Value.TotalSeconds);
    }
}
