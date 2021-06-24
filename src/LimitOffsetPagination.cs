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
            // Setting basic data
            var numberOfRowsToSkip = RetrieveConfiguredOffset(offsetQueryParam.Value);
            var numberOfRowsToTake = RetrieveConfiguredLimit(limitQueryParam.Value);
            var count = await source.CountAsync();
            var nextLink = RetrieveNextLink(url, numberOfRowsToSkip, numberOfRowsToTake, count);
            var previousLink = RetrievePreviousLink(url, numberOfRowsToSkip, numberOfRowsToTake);
            // Building list
            IQueryable<T> filteredSource = FilterIfApplicable(source, allOthersParams);
            var items = await filteredSource.Skip(numberOfRowsToSkip).Take(numberOfRowsToTake).ToListAsync();

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

        private string? RetrievePreviousLink(string url, int numberOfRowsToSkip, int numberOfRowsToTake)
        {
            if (numberOfRowsToSkip == 0)
                return null;

            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
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

        private string? RetrieveNextLink(string url, int numberOfRowsToSkip, int numberOfRowsToTake, int count)
        {
            var greaterThanTheAmountOfRowsAvailable = numberOfRowsToSkip + numberOfRowsToTake >= count;
            if (greaterThanTheAmountOfRowsAvailable) return null;

            var newOffSetValue = numberOfRowsToSkip + numberOfRowsToTake;

            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
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

        private IQueryable<T> FilterIfApplicable<T>(IQueryable<T> source,
            IEnumerable<KeyValuePair<string, StringValues>> queryParams)
        {
            var propertiesToBeUsed = new Dictionary<PropertyInfo, string>();
            var typeOfTheGivenGeneric = typeof(T);
            var flags = BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance;

            // Get all valid properties that were provided
            foreach (var queryParam in queryParams)
            {
                var propertyName = queryParam.Key;
                var propertyValue = queryParam.Value.ToString();
                var property = typeOfTheGivenGeneric.GetProperty(propertyName, flags);
                if (property is not null && propertyValue is not null)
                    propertiesToBeUsed.Add(property, queryParam.Value.ToString());
            }

            var shouldApplyFiltering = propertiesToBeUsed.Count > 0;

            if (shouldApplyFiltering)
            {
                var parameterExpression = Expression.Parameter(typeOfTheGivenGeneric);
                var allPredicates = new List<Expression<Func<T, bool>>>();

                // Create all predicates
                foreach (var keyValuePair in propertiesToBeUsed)
                {
                    // Extract details
                    var propertyInfo = keyValuePair.Key;
                    var propertyType = propertyInfo.PropertyType;
                    var value = keyValuePair.Value;
                    // Create the expression
                    var propertyOrFieldTarget = Expression.PropertyOrField(parameterExpression, propertyInfo.Name);
                    try
                    {
                        var castedValue = Convert.ChangeType(value, propertyType);
                        var valueToBeEqual = Expression.Constant(castedValue, propertyType);
                        var finalExpression = Expression.Equal(propertyOrFieldTarget, valueToBeEqual);
                        var predicate = Expression.Lambda<Func<T, bool>>(finalExpression, parameterExpression);
                        // Add to list of predicates
                        allPredicates.Add(predicate);
                    }
                    catch (FormatException)
                    {
                        // It happens let's say when you try to convert ABC to int, thus raising FormatException 😉
                    }
                }

                var hasPredicates = allPredicates.Count > 0;
                if (hasPredicates)
                {
                    // Merge all predicates created
                    Expression<Func<T, bool>> mergedPredicate = allPredicates.First();
                    foreach (var (predicate, index) in allPredicates.Select((item, index) => (item, index)))
                    {
                        if (index is not 0)
                        {
                            InvocationExpression invocationExpression =
                                Expression.Invoke(predicate, mergedPredicate.Parameters);
                            mergedPredicate = Expression.Lambda<Func<T, bool>>(
                                Expression.AndAlso(mergedPredicate.Body, invocationExpression), predicate.Parameters);
                        }
                    }

                    return source.Where(mergedPredicate);
                }
            }

            return source;
        }
    }
}
