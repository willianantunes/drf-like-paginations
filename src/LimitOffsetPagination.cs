using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace DrfLikePaginations
{
    public class LimitOffsetPagination : PaginationBase
    {
        private readonly int _defaultLimit;
        private readonly int _maxPageSize;
        private readonly string _limitQueryParam = "limit";
        private readonly string _offsetQueryParam = "offset";

        public LimitOffsetPagination(int defaultPageSize, int maxPageSize = 25) : base(defaultPageSize, maxPageSize)
        {
            _defaultLimit = defaultPageSize;
            _maxPageSize = maxPageSize;
        }

        public override async Task<Paginated<T>> CreateAsync<T>(IQueryable<T> source, string url, IQueryCollection queryParams)
        {
            // Extracting query strings
            var limitQueryParam = queryParams.FirstOrDefault(pair => pair.Key == _limitQueryParam);
            var offsetQueryParam = queryParams.FirstOrDefault(pair => pair.Key == _offsetQueryParam);
            var allOthersParams =
                queryParams.Where(pair => pair.Key != _limitQueryParam || pair.Key != _offsetQueryParam);
            // Basic data
            var numberOfRowsToSkip = RetrieveConfiguredOffset(offsetQueryParam.Value);
            var numberOfRowsToTake = RetrieveConfiguredLimit(limitQueryParam.Value);
            // Building list
            var (paramsForFiltering, filteredSource) = ApplyCustomFilterIfApplicable(source, allOthersParams);
            var count = await filteredSource.CountAsync();
            var items = await filteredSource.Skip(numberOfRowsToSkip).Take(numberOfRowsToTake).ToListAsync();
            // Links
            var nextLink = RetrieveNextLink(url, numberOfRowsToSkip, numberOfRowsToTake, count, paramsForFiltering);
            var previousLink = RetrievePreviousLink(url, numberOfRowsToSkip, numberOfRowsToTake, paramsForFiltering);

            return new Paginated<T>(count, nextLink, previousLink, items);
        }

        public override async Task<Paginated<TResult>> CreateAsync<T, TResult>(IQueryable<T> source, string url,
            IQueryCollection queryParams, Func<T, TResult> transform)
        {
            var paginated = await CreateAsync(source, url, queryParams);
            var paginatedResults = paginated.Results;
            var refreshedResults = paginatedResults.Select(transform);

            return new Paginated<TResult>(paginated.Count, paginated.Next, paginated.Previous, refreshedResults);
        }

        private string? RetrievePreviousLink(string url, int numberOfRowsToSkip, int numberOfRowsToTake,
            List<KeyValuePair<string, StringValues>> paramsForFiltering)
        {
            if (numberOfRowsToSkip == 0)
                return null;

            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            // When you add some filters, we must repass the valid ones
            foreach (var paramForFiltering in paramsForFiltering)
            {
                var key = paramForFiltering.Key;
                var value = paramForFiltering.Value[0];
                query.Add(key, value);
            }
            query[_limitQueryParam] = numberOfRowsToTake.ToString();

            var shouldNotProvideOffset = numberOfRowsToSkip - numberOfRowsToTake <= 0;
            if (shouldNotProvideOffset)
            {
                uriBuilder.Query = query.ToString();
                return uriBuilder.Uri.AbsoluteUri;
            }

            var newOffSetValue = numberOfRowsToSkip - numberOfRowsToTake;

            query[_offsetQueryParam] = newOffSetValue.ToString();
            uriBuilder.Query = query.ToString();

            return uriBuilder.Uri.AbsoluteUri;
        }

        private string? RetrieveNextLink(string url, int numberOfRowsToSkip, int numberOfRowsToTake, int count,
            List<KeyValuePair<string, StringValues>> paramsForFiltering)
        {
            var greaterThanTheAmountOfRowsAvailable = numberOfRowsToSkip + numberOfRowsToTake >= count;
            if (greaterThanTheAmountOfRowsAvailable) return null;

            var newOffSetValue = numberOfRowsToSkip + numberOfRowsToTake;

            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            // When you add some filters, we must repass the valid ones
            foreach (var paramForFiltering in paramsForFiltering)
            {
                var key = paramForFiltering.Key;
                var value = paramForFiltering.Value[0];
                query.Add(key, value);
            }
            query[_limitQueryParam] = numberOfRowsToTake.ToString();
            query[_offsetQueryParam] = newOffSetValue.ToString();
            uriBuilder.Query = query.ToString();

            return uriBuilder.Uri.AbsoluteUri;
        }

        private int RetrieveConfiguredOffset(StringValues values)
        {
            int defaultOffSetValue = 0;
            var value = values.FirstOrDefault();

            if (value is not null)
            {
                int requestedOffsetValue;
                var couldBeParsed = int.TryParse(value, out requestedOffsetValue);

                if (couldBeParsed && requestedOffsetValue > 0)
                    return requestedOffsetValue;
            }

            return defaultOffSetValue;
        }
    }
}
