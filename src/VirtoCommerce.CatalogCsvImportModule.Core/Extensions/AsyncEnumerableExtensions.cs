using System.Collections.Generic;

namespace VirtoCommerce.CatalogCsvImportModule.Core.Extensions;

public static class AsyncEnumerableExtensions
{
    public static async IAsyncEnumerable<IList<T>> Paginate<T>(this IAsyncEnumerable<T> source, int batchSize)
    {
        var page = new List<T>();

        await foreach (var item in source)
        {
            page.Add(item);

            if (page.Count >= batchSize)
            {
                yield return page;
                page.Clear();
            }
        }

        if (page.Count > 0)
        {
            yield return page;
        }
    }
}
