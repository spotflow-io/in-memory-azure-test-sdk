using System.Diagnostics;

using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Spotflow.InMemory.Azure.Storage.FluentAssertions;

public class BlobBaseClientAssertions(BlobBaseClient subject)
    : ReferenceTypeAssertions<BlobBaseClient, BlobBaseClientAssertions>(subject)
{
    protected override string Identifier => nameof(BlobBaseClient);

    /// <summary>
    /// Checks that the blob exists. If <paramref name="waitTime"/> is provided, it will wait up the the specified time for the blob to exist before failing.
    /// </summary>
    [CustomAssertion]
    public AndConstraint<BlobBaseClientAssertions> Exist(TimeSpan? waitTime = null, string? because = null, params object[] becauseArgs)
    {
        using var scope = StartScope(because, becauseArgs);

        if (waitTime is null)
        {
            if (!Subject.Exists())
            {
                scope.FailWith("Expected blob to exist{reason} but it does not.");
            }

            return new(this);
        }

        var startTime = Stopwatch.GetTimestamp();

        while (Stopwatch.GetElapsedTime(startTime) < waitTime)
        {
            try
            {
                if (Subject.Exists())
                {
                    return new(this);
                }
            }
            catch (RequestFailedException) { }

            Thread.Sleep(10);
        }

        scope.FailWith("Expected blob to exist{reason} eventually but it does not exist event after {0} seconds.", waitTime.Value.TotalSeconds);

        return new(this);

    }

    [CustomAssertion]
    public AndConstraint<BlobBaseClientAssertions> MatchName(string expectedNamePattern, string? because = null, params object[] becauseArgs)
    {
        using var scope = StartScope($"name of the blob", because, becauseArgs);

        var actualBlobName = Subject.Name;

        actualBlobName.Should().Match(expectedNamePattern);

        return new(this);
    }

    #region Content

    [CustomAssertion]
    public AndConstraint<BlobBaseClientAssertions> HaveSize(long expectedSize, string? because = null, params object[] becauseArgs)
    {
        using var scope = StartScope($"size of the blob", because, becauseArgs);

        var actualSize = Subject.GetProperties().Value.ContentLength;

        actualSize.Should().Be(expectedSize);

        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<BlobBaseClientAssertions> BeEmpty(string? because = null, params object[] becauseArgs)
    {
        return HaveSize(0, because: because, becauseArgs: becauseArgs);
    }

    [CustomAssertion]
    public AndWhichConstraint<BlobBaseClientAssertions, string> HaveContent(string expectedContent, string? because = null, params object[] becauseArgs)
    {
        using var scope = StartScope($"content of the blob", because, becauseArgs);

        var actualContent = Subject.DownloadContent().Value.Content.ToString();

        actualContent.Should().Be(expectedContent, because: because, becauseArgs: becauseArgs);

        return new(this, actualContent);
    }

    #endregion

    #region Blocks

    [CustomAssertion]
    public AndConstraint<BlobBaseClientAssertions> HaveCommittedBlocks(int expectedCount, string? because = null, params object[] becauseArgs)
    {
        using var scope = StartScope($"number of committed blocks in the blob '{Subject.Uri}'", because, becauseArgs);

        var actualBlocks = GetBlockList().CommittedBlocks.ToList();

        actualBlocks.Should().HaveCount(expectedCount);

        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<BlobBaseClientAssertions> HaveNoCommittedBlocks(string? because = null, params object[] becauseArgs)
    {
        return HaveCommittedBlocks(0, because: because, becauseArgs: becauseArgs);
    }

    [CustomAssertion]
    public AndConstraint<BlobBaseClientAssertions> HaveUncommittedBlocks(int expectedCount, string? because = null, params object[] becauseArgs)
    {
        using var scope = StartScope($"number of uncommitted blocks in the blob '{Subject.Uri}'", because, becauseArgs);

        var actualBlocks = GetBlockList().UncommittedBlocks.ToList();

        actualBlocks.Should().HaveCount(expectedCount);

        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<BlobBaseClientAssertions> HaveNoUncommittedBlocks(string? because = null, params object[] becauseArgs)
    {
        return HaveUncommittedBlocks(0, because: because, becauseArgs: becauseArgs);
    }


    /// <summary>
    /// Asserts that blob has expected number of committed blocks with expected sizes.
    /// </summary>
    /// <param name="expectedBlockSizes">
    /// Expected sizes of individual blocks in the block list order.
    /// If an item in the array is set to <c>null</c>, the size of the particular block is not checked.
    /// </param>
    /// <returns></returns>
    [CustomAssertion]
    public AndConstraint<BlobBaseClientAssertions> HaveCommittedBlocksWithSizes(int?[] expectedBlockSizes, string? because = null, params object[] becauseArgs)
    {
        var blocks = GetBlockList().CommittedBlocks.ToList();

        HaveCommittedBlocks(expectedBlockSizes.Length, because: because, becauseArgs: becauseArgs);

        using (var scope = StartScope(because, becauseArgs))
        {
            for (var i = 0; i < expectedBlockSizes.Length; i++)
            {
                var expectedBlockSize = expectedBlockSizes[i];
                if (expectedBlockSize.HasValue)
                {
                    scope.Context = new($"block #{i} size");

                    var actualBlockSize = blocks[i].Size;

                    actualBlockSize.Should().Be(expectedBlockSize.Value);
                }
            }
        }

        return new(this);
    }

    [CustomAssertion]
    public AndConstraint<BlobBaseClientAssertions> HaveCommittedBlock(int blockOrdinal, Action<BlobBlock> blockAssertion, string? because = null, params object[] becauseArgs)
    {
        using var scope = StartScope(because, becauseArgs);

        var blocks = GetBlockList().CommittedBlocks.ToList();

        var actualBlockCount = blocks.Count;
        var minimalExpectedBlockCount = blockOrdinal + 1;

        scope.AddReportable("Block", $"#{blockOrdinal}");

        actualBlockCount.Should().BeGreaterOrEqualTo(minimalExpectedBlockCount);

        var actualBlock = blocks[blockOrdinal];

        blockAssertion(actualBlock);

        return new(this);
    }

    #endregion

    private AssertionScope StartScope(string? because, object[] becauseArgs)
    {
        var scope = new AssertionScope();
        return StartScopeCore(because, becauseArgs, scope);
    }

    private AssertionScope StartScope(string context, string? because, object[] becauseArgs)
    {
        var scope = new AssertionScope(context);
        return StartScopeCore(because, becauseArgs, scope);
    }

    private AssertionScope StartScopeCore(string? because, object[] becauseArgs, AssertionScope scope)
    {
        scope.BecauseOf(because, becauseArgs);
        scope.AddReportable("Blob", Subject.Uri.ToString());
        return scope;
    }

    private BlockList GetBlockList()
    {
        var container = Subject.GetParentBlobContainerClient();

        var client = container.GetBlockBlobClient(Subject.Name);

        return client.GetBlockList(BlockListTypes.All).Value;

    }
}
