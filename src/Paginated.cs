using System.Collections.Generic;

namespace DrfLikePaginations
{
    public record Paginated<T>(int Count, string? Next, string? Previous, IEnumerable<T> Results);
}
