using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace DrfLikePaginations
{
    record CursorDetails(bool Reverse, string? CurrentPosition);

    public class CursorPagination : PaginationBase
    {
        private readonly string _defaultFieldForOrdering;
        private readonly string _cursorQueryParam = "cursor";

        public CursorPagination(int defaultPageSize, int maxPageSize = 25, string defaultFieldForOrdering = "Id") :
            base(defaultPageSize, maxPageSize)
        {
            _defaultFieldForOrdering = defaultFieldForOrdering;
        }

        public override async Task<Paginated<T>> CreateAsync<T>(IQueryable<T> source, string url,
            IQueryCollection queryParams)
        {
            // Extracting query strings
            var cursorQueryParam = queryParams.FirstOrDefault(pair => pair.Key == _cursorQueryParam);
            var limitQueryParam = queryParams.FirstOrDefault(pair => pair.Key == _limitQueryParam);
            // Basic data
            var cursor = RetrieveConfiguredCursor(cursorQueryParam.Value);
            var numberOfRowsToTake = RetrieveConfiguredLimit(limitQueryParam.Value);
            // Building list
            // TODO: Add OFFSET to fix possible collisions
            var orderedSource = ApplyOrdering(source);
            var filteredSource = ApplyFilterIfRequired(orderedSource, cursor);
            var extraItemToIdentifyNextPage = 1;
            var actualNumberOfRowsToTake = numberOfRowsToTake + extraItemToIdentifyNextPage;
            var items = await filteredSource.Take(actualNumberOfRowsToTake).ToListAsync();
            var itemToBeReturned = items.Take(numberOfRowsToTake);
            // What is needed to build the links
            var indexElementFromNextPage = actualNumberOfRowsToTake - 1;
            var indexElementBeforeTheOneFromNextPage = actualNumberOfRowsToTake - 2;
            var hasElementNextPage = items.ElementAtOrDefault(indexElementFromNextPage) is not null;
            string? positionToBeUsedNextLink = null;
            if (hasElementNextPage)
            {
                var lastElement = items.ElementAt(indexElementBeforeTheOneFromNextPage)!;
                positionToBeUsedNextLink =
                    Reflections.RetrieveValueAsString(lastElement, typeof(T), _defaultFieldForOrdering);
            }

            var currentPosition = cursor.CurrentPosition;
            // Building links
            string? nextLink = RetrieveNextLink(url, numberOfRowsToTake, positionToBeUsedNextLink);
            string? previousLink = RetrievePreviousLink(url, numberOfRowsToTake, currentPosition);

            return new Paginated<T>(null, nextLink, previousLink, itemToBeReturned);
        }

        public override Task<Paginated<TResult>> CreateAsync<T, TResult>(IQueryable<T> source, string url,
            IQueryCollection queryParams, Func<T, TResult> transform)
        {
            throw new NotImplementedException();
        }

        private string? RetrievePreviousLink(string url, int numberOfRowsToTake, string? cursorPosition)
        {
            if (cursorPosition is null)
                return null;

            // Notice that the reverse here is set as TRUE
            var newCursor = new CursorDetails(true, cursorPosition);

            // Cursor encoded query string
            var uriBuilderForCursorOnly = new UriBuilder();
            var cursorDetailsParams = HttpUtility.ParseQueryString(uriBuilderForCursorOnly.Query);
            cursorDetailsParams["r"] = newCursor.Reverse.ToString();
            cursorDetailsParams["p"] = newCursor.CurrentPosition;
            uriBuilderForCursorOnly.Query = cursorDetailsParams.ToString();
            var cursorQueryString = uriBuilderForCursorOnly.Uri.PathAndQuery;
            var cleanedCursorQueryString = String.Join("", cursorQueryString.Split("?")[1]);
            var encodedCursorQueryString = Base64.Encode(cleanedCursorQueryString);

            // Now the link itself
            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query[_cursorQueryParam] = encodedCursorQueryString;
            query[_limitQueryParam] = numberOfRowsToTake.ToString();
            uriBuilder.Query = query.ToString();

            return uriBuilder.Uri.AbsoluteUri;
        }

        private string? RetrieveNextLink(string url, int numberOfRowsToTake, string? cursorPosition)
        {
            if (cursorPosition is null)
                return null;
            
            // Notice that the reverse here is set as FALSE
            var newCursor = new CursorDetails(false, cursorPosition);

            // Cursor encoded query string
            var uriBuilderForCursorOnly = new UriBuilder();
            var cursorDetailsParams = HttpUtility.ParseQueryString(uriBuilderForCursorOnly.Query);
            cursorDetailsParams["r"] = newCursor.Reverse.ToString();
            cursorDetailsParams["p"] = newCursor.CurrentPosition;
            uriBuilderForCursorOnly.Query = cursorDetailsParams.ToString();
            var cursorQueryString = uriBuilderForCursorOnly.Uri.PathAndQuery;
            var cleanedCursorQueryString = String.Join("", cursorQueryString.Split("?")[1]);
            var encodedCursorQueryString = Base64.Encode(cleanedCursorQueryString);

            // Now the link itself
            var uriBuilder = new UriBuilder(url);
            var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            query[_cursorQueryParam] = encodedCursorQueryString;
            query[_limitQueryParam] = numberOfRowsToTake.ToString();
            uriBuilder.Query = query.ToString();

            return uriBuilder.Uri.AbsoluteUri;
        }

        private CursorDetails RetrieveConfiguredCursor(StringValues values)
        {
            var valueAsBase64 = values.FirstOrDefault();

            if (valueAsBase64 is not null)
            {
                var valueAsString = Base64.Decode(valueAsBase64);
                var queryStringCollection = HttpUtility.ParseQueryString(valueAsString);
                Trace.Assert(queryStringCollection.Keys.Count == 2);

                var reverse = bool.Parse(queryStringCollection["r"]!);
                var position = queryStringCollection["p"];

                return new CursorDetails(reverse, position);
            }

            return new CursorDetails(false, null);
        }

        private IQueryable<T> ApplyOrdering<T>(IQueryable<T> data)
        {
            var typeOfTheGivenGeneric = typeof(T);
            var property =
                Reflections.RetrievePropertyInfoFromSourceGivenItsName(typeOfTheGivenGeneric, _defaultFieldForOrdering);

            var param = Expression.Parameter(typeof(T));
            var memberAccess = Expression.Property(param, property.Name);
            var convertedMemberAccess = Expression.Convert(memberAccess, typeof(object));
            var orderPredicate = Expression.Lambda<Func<T, object>>(convertedMemberAccess, param);

            return data.AsQueryable().OrderBy(orderPredicate);
        }

        private IQueryable<T> ApplyFilterIfRequired<T>(IQueryable<T> data, CursorDetails cursor)
        {
            var currentPosition = cursor.CurrentPosition;
            var reverse = cursor.Reverse;

            if (string.IsNullOrEmpty(currentPosition) is false)
            {
                var typeOfTheGivenGeneric = typeof(T);
                var property =
                    Reflections.RetrievePropertyInfoFromSourceGivenItsName(typeOfTheGivenGeneric,
                        _defaultFieldForOrdering);
                var propertyType = property.PropertyType;

                // First we build the left expression, which is the property itself
                var param = Expression.Parameter(typeOfTheGivenGeneric);
                var propertyOrFieldTarget = Expression.PropertyOrField(param, property.Name);
                var convertedMemberAccess = Expression.Convert(propertyOrFieldTarget, propertyType);
                // Now we create the right, which is the value to be compared with
                var castedValue = Convert.ChangeType(currentPosition!, propertyType);
                var currentPositionExpression = Expression.Constant(castedValue, propertyType);
                BinaryExpression lessThanOrGreaterThanExpression = reverse is true
                    ? Expression.LessThan(convertedMemberAccess, currentPositionExpression)
                    : Expression.GreaterThan(convertedMemberAccess, currentPositionExpression);
                // Final expression
                var filterExpression = Expression.Lambda<Func<T, bool>>(lessThanOrGreaterThanExpression, param);

                return data.Where(filterExpression);
            }

            return data;
        }
    }
}