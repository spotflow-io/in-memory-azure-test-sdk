using System.Diagnostics;
using System.Linq.Expressions;

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

    public AndWhichConstraint<BlobBaseClientAssertions, string> HaveStringContent(string expectedContent)
    {
        var content = Subject.DownloadContent().Value.Content.ToString();

        Execute
            .Assertion
            .ForCondition(expectedContent == content)
            .FailWith("Expected blob content to be '{0}', but found '{1}'", expectedContent, content);

        return new(this, content);
    }

    public AndConstraint<BlobBaseClientAssertions> HaveName(string expectedNamePattern)
    {
        Subject.Name.Should().Match(expectedNamePattern);
        return new(this);
    }

    public AndConstraint<BlobBaseClientAssertions> HaveSize(long expectedSize)
    {
        var container = Subject.GetParentBlobContainerClient();

        var client = container.GetBlobClient(Subject.Name);

        var content = client.DownloadContent().Value.Content;

        var longLength = (long) content.ToMemory().Length;

        longLength.Should().Be(expectedSize);

        return new(this);
    }


    public AndConstraint<BlobBaseClientAssertions> HaveCommittedBlocks(int expectedCount)
    {
        var container = Subject.GetParentBlobContainerClient();

        var client = container.GetBlockBlobClient(Subject.Name);

        var actualBlocks = client.GetBlockList(BlockListTypes.Committed).Value.CommittedBlocks.ToList();

        actualBlocks.Should().HaveCount(expectedCount);

        return new(this);
    }


    public AndConstraint<BlobBaseClientAssertions> HaveNoCommittedBlocks()
    {
        return HaveCommittedBlocks(0);
    }


    public AndConstraint<BlobBaseClientAssertions> HaveUncommittedBlocks(int expectedCount)
    {
        var container = Subject.GetParentBlobContainerClient();

        var client = container.GetBlockBlobClient(Subject.Name);

        var actualBlocks = client.GetBlockList(BlockListTypes.Uncommitted).Value.UncommittedBlocks.ToList();

        actualBlocks.Should().HaveCount(expectedCount);

        return new(this);
    }

    public AndConstraint<BlobBaseClientAssertions> HaveNoUncommittedBlocks()
    {
        return HaveUncommittedBlocks(0);
    }


    public AndConstraint<BlobBaseClientAssertions> HaveCommittedBlocksWithSizes(int?[] expectedBlockSizes)
    {
        var container = Subject.GetParentBlobContainerClient();

        var client = container.GetBlockBlobClient(Subject.Name);

        var blockList = client.GetBlockList(BlockListTypes.Committed).Value;

        var blocks = blockList.CommittedBlocks.ToList();

        if (blocks.Count != expectedBlockSizes.Length)
        {
            Execute
                .Assertion
                .ForCondition(false)
                .FailWith("Expected {0} committed blocks, but found {1}", expectedBlockSizes.Length, blocks.Count);
        }

        using (var scope = new AssertionScope())
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

    public AndConstraint<BlobBaseClientAssertions> HaveCommittedBlock(int blockOrdinal, Expression<Func<BlobBlock, bool>> matcherExpression)
    {
        var container = Subject.GetParentBlobContainerClient();

        var client = container.GetBlockBlobClient(Subject.Name);

        var blockList = client.GetBlockList(BlockListTypes.Committed).Value;

        var blocks = blockList.CommittedBlocks.ToList();

        if (blocks.Count <= blockOrdinal)
        {
            Execute.Assertion
                .ForCondition(false)
                .FailWith("Expected at least {0} committed blocks, but found {1}", blockOrdinal + 1, blocks.Count);
        }

        var actualBlock = blocks[blockOrdinal];

        var matcher = matcherExpression.Compile();

        Execute.Assertion
            .ForCondition(matcher(actualBlock))
            .FailWith("Expected block #{0} to match the condition {1}, but it did not", blockOrdinal, matcherExpression);

        return new(this);
    }

    public AndConstraint<BlobBaseClientAssertions> BeEmpty()
    {
        var content = Subject.DownloadContent().Value.Content.ToMemory();

        content.Length.Should().Be(0);

        return new(this);
    }
}
