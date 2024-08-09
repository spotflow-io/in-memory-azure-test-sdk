using System.Diagnostics;

using Azure;
using Azure.Storage.Blobs.Specialized;

using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Spotflow.InMemory.Azure.Storage.FluentAssertions;

public class BlobBaseClientAssertions(BlobBaseClient subject)
    : ReferenceTypeAssertions<BlobBaseClient, BlobBaseClientAssertions>(subject)
{
    protected override string Identifier => nameof(BlobBaseClient);

    public async Task ExistAsync(TimeSpan? maxWaitTime = null, string? because = null, params object[] becauseArgs)
    {
        maxWaitTime ??= TimeSpan.FromSeconds(8);

        var startTime = Stopwatch.GetTimestamp();

        while (Stopwatch.GetElapsedTime(startTime) < maxWaitTime)
        {
            try
            {
                if (await Subject.ExistsAsync())
                {
                    return;
                }
            }
            catch (RequestFailedException) { }

            await Task.Delay(10);
        }

        Execute
            .Assertion
            .BecauseOf(because, becauseArgs)
            .FailWith("Blob {0} should exist{reason} but it does not exist event after {1} seconds.", Subject.Uri, maxWaitTime.Value.TotalSeconds);
    }
}
