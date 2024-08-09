using Azure;

namespace Spotflow.InMemory.Azure.Internals;

internal static class InMemoryPageable
{
    internal class YieldingAsync<T>(IReadOnlyList<T> items, int maxPageSize) : AsyncPageable<T> where T : notnull
    {
        private readonly int _maxPageSize = maxPageSize <= 0 ? throw new ArgumentOutOfRangeException(nameof(maxPageSize)) : maxPageSize;

        public override async IAsyncEnumerable<Page<T>> AsPages(string? continuationToken = null, int? pageSizeHint = null)
        {
            if (pageSizeHint <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSizeHint));
            }

            await Task.Yield();

            var pages = PreparePages(items, _maxPageSize, pageSizeHint);

            var shouldReturnPage = continuationToken is null;

            foreach (var page in pages)
            {
                if (shouldReturnPage)
                {
                    await Task.Yield();
                    yield return page;
                }
                else if (page.ContinuationToken == continuationToken)
                {
                    shouldReturnPage = true;
                }
            }
        }
    }

    public class Sync<T>(IReadOnlyList<T> items, int maxPageSize) : Pageable<T> where T : notnull
    {
        private readonly int _maxPageSize = maxPageSize <= 0 ? throw new ArgumentOutOfRangeException(nameof(maxPageSize)) : maxPageSize;

        public override IEnumerable<Page<T>> AsPages(string? continuationToken = null, int? pageSizeHint = null)
        {
            if (pageSizeHint <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pageSizeHint));
            }

            var pages = PreparePages(items, _maxPageSize, pageSizeHint);

            var shouldReturnPage = continuationToken is null;

            foreach (var page in pages)
            {
                if (shouldReturnPage)
                {
                    yield return page;
                }
                else if (page.ContinuationToken == continuationToken)
                {
                    shouldReturnPage = true;
                }
            }
        }
    }


    private static IEnumerable<Page<T>> PreparePages<T>(IReadOnlyList<T> items, int maxPageSize, int? pageSizeHint)
    {
        if (pageSizeHint <= 0)
        {
            throw new InvalidOperationException(nameof(pageSizeHint));
        }

        var pageSize = Math.Min(pageSizeHint ?? maxPageSize, maxPageSize);

        var numberOfPages = (int) Math.Ceiling((double) items.Count / pageSize);

        for (var pageIndex = 0; pageIndex < numberOfPages; pageIndex++)
        {
            var isLastPage = pageIndex == numberOfPages - 1;

            var continuationToken = isLastPage ? null : $"page-{pageIndex + 1}";

            var itemsInPage = items.Skip(pageIndex * pageSize).Take(pageSize).ToList();

            var page = Page<T>.FromValues(itemsInPage, continuationToken, new InMemoryResponse(200));

            yield return page;
        }

    }
}


