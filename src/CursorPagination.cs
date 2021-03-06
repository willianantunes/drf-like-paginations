using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;

namespace DrfLikePaginations
{
    record CursorDetails(bool Reverse, string? CurrentPosition);

    record Positions(string? Previous, string? Next);

    public class CursorPagination : PaginationBase
    {
        private readonly string _defaultFieldForOrdering;
        private readonly string _cursorQueryParam = "cursor";
        private readonly bool _useDescOrdering;

        public CursorPagination(int defaultPageSize, int maxPageSize = 25, string defaultFieldForOrdering = "Id") :
            base(defaultPageSize, maxPageSize)
        {
            var pattern = @"^-?([a-zA-Z]+)$";
            var match = Regex.Match(defaultFieldForOrdering, pattern);

            if (match.Success)
            {
                _useDescOrdering = defaultFieldForOrdering.StartsWith("-");
                _defaultFieldForOrdering = match.Groups[1].Value;
            }
            else
            {
                var message = $"The field {defaultFieldForOrdering} does not match the pattern: {pattern}";
                throw new ProvidedFieldForOrderingIsWrongException(message);
            }
        }

        public override async Task<Paginated<T>> CreateAsync<T>(IQueryable<T> source, string url,
            IQueryCollection queryParams)
        {
            // Extracting query strings
            var cursorQueryParam = queryParams.FirstOrDefault(pair => pair.Key == _cursorQueryParam);
            var limitQueryParam = queryParams.FirstOrDefault(pair => pair.Key == _limitQueryParam);
            var allOthersParams = queryParams.Where(pair => pair.Key != _limitQueryParam);
            // Basic data
            var cursor = RetrieveConfiguredCursor(cursorQueryParam.Value);
            var numberOfRowsToTake = RetrieveConfiguredLimit(limitQueryParam.Value);
            // Building list
            var (paramsForFiltering, customSource) = ApplyCustomFilterIfApplicable(source, allOthersParams);
            // var customSource = ApplyCustomFilterIfApplicable(source, allOthersParams);
            var orderedSource = ApplyOrdering(customSource, cursor);
            var filteredSource = ApplyFilterIfRequired(orderedSource, cursor);
            var extraItemToIdentifyNextPage = 1;
            var actualNumberOfRowsToTake = numberOfRowsToTake + extraItemToIdentifyNextPage;
            var items = await filteredSource.Take(actualNumberOfRowsToTake).ToListAsync();
            var itemsToBeReturned = items.Take(numberOfRowsToTake).ToList();
            // What is needed to build the links
            if (cursor.Reverse)
            {
                // So the user can see it as expected
                items.Reverse();
                itemsToBeReturned.Reverse();
            }
            // TODO: Add OFFSET to fix possible collisions
            var positions = RetrievePositions(cursor, actualNumberOfRowsToTake, items, itemsToBeReturned);
            // Building links
            string? previousLink = RetrievePreviousLink(url, numberOfRowsToTake, positions.Previous, paramsForFiltering);
            string? nextLink = RetrieveNextLink(url, numberOfRowsToTake, positions.Next, paramsForFiltering);

            return new Paginated<T>(null, nextLink, previousLink, itemsToBeReturned);
        }

        public override async Task<Paginated<TResult>> CreateAsync<T, TResult>(IQueryable<T> source, string url,
            IQueryCollection queryParams, Func<T, TResult> transform)
        {
            var paginated = await CreateAsync(source, url, queryParams);
            var paginatedResults = paginated.Results;
            var refreshedResults = paginatedResults.Select(transform);

            return new Paginated<TResult>(paginated.Count, paginated.Next, paginated.Previous, refreshedResults);
        }

        private string? RetrievePreviousLink(string url, int numberOfRowsToTake, string? cursorPosition,
            List<KeyValuePair<string, StringValues>> paramsForFiltering)
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
            // When you add some filters, we must repass the valid ones
            foreach (var paramForFiltering in paramsForFiltering)
            {
                var key = paramForFiltering.Key;
                var value = paramForFiltering.Value[0];
                query.Add(key, value);
            }
            query[_cursorQueryParam] = encodedCursorQueryString;
            query[_limitQueryParam] = numberOfRowsToTake.ToString();
            uriBuilder.Query = query.ToString();

            return uriBuilder.Uri.AbsoluteUri;
        }

        private string? RetrieveNextLink(string url, int numberOfRowsToTake, string? cursorPosition,
            List<KeyValuePair<string, StringValues>> paramsForFiltering)
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
            // When you add some filters, we must repass the valid ones
            foreach (var paramForFiltering in paramsForFiltering)
            {
                var key = paramForFiltering.Key;
                var value = paramForFiltering.Value[0];
                query.Add(key, value);
            }
            query[_cursorQueryParam] = encodedCursorQueryString;
            query[_limitQueryParam] = numberOfRowsToTake.ToString();
            uriBuilder.Query = query.ToString();

            return uriBuilder.Uri.AbsoluteUri;
        }

        private CursorDetails RetrieveConfiguredCursor(StringValues values)
        {
            var valueAsBase64 = values.FirstOrDefault();

            try
            {
                if (valueAsBase64 is not null)
                {
                    var valueAsString = Base64.Decode(valueAsBase64);
                    var queryStringCollection = HttpUtility.ParseQueryString(valueAsString);
                    Trace.Assert(queryStringCollection.Keys.Count == 2);

                    var reverse = bool.Parse(queryStringCollection["r"]!);
                    var position = queryStringCollection["p"];

                    return new CursorDetails(reverse, position);
                }
            }
            catch (FormatException)
            {
                // When the input is not a valid Base-64 string
            }

            return new CursorDetails(false, null);
        }

        private Positions RetrievePositions<T>(CursorDetails cursor, int actualNumberOfRowsToTake, List<T> items,
            List<T> itemsToBeReturned)
        {
            var genericType = typeof(T);
            // Is there any position that should be followed?
            var hasFollowingPosition = items.Count > itemsToBeReturned.Count;
            string? followingPosition = null;
            if (hasFollowingPosition is true)
            {
                var item = cursor.Reverse ? items.First()! : items.Last()!;
                followingPosition = Reflections.RetrieveValueAsString(item, genericType, _defaultFieldForOrdering);
            }

            // The previous and next links are changed depending on the reverse order
            Func<string?, bool, string?> retrieveValidPosition = (positionToBeEvaluated, invertItems) =>
            {
                if (positionToBeEvaluated is null)
                    return null;
                var enumerableOfItems = itemsToBeReturned.AsEnumerable();
                if (invertItems)
                    enumerableOfItems = enumerableOfItems.Reverse();

                foreach (var item in enumerableOfItems)
                {
                    var position = Reflections.RetrieveValueAsString(item, genericType, _defaultFieldForOrdering);

                    if (position != positionToBeEvaluated)
                        return position;

                    var message = $"The position {position} should have been different than {cursor.CurrentPosition}";
                    throw new OffsetFeatureNotImplementedException(message);
                }

                return null;
            };
            if (cursor.Reverse is false)
            {
                var previousPosition = retrieveValidPosition(cursor.CurrentPosition, false);
                var nextPosition = retrieveValidPosition(followingPosition, true);
                return new Positions(previousPosition, nextPosition);
            }
            else
            {
                var previousPosition = retrieveValidPosition(followingPosition, false);
                var nextPosition = retrieveValidPosition(cursor.CurrentPosition, true);
                return new Positions(previousPosition, nextPosition);
            }
        }

        private IQueryable<T> ApplyOrdering<T>(IQueryable<T> source, CursorDetails cursor)
        {
            var typeOfTheGivenGeneric = typeof(T);
            var property =
                Reflections.RetrievePropertyInfoFromSourceGivenItsName(typeOfTheGivenGeneric, _defaultFieldForOrdering);

            var param = Expression.Parameter(typeof(T));
            var memberAccess = Expression.Property(param, property.Name);
            var convertedMemberAccess = Expression.Convert(memberAccess, typeof(object));
            var orderPredicate = Expression.Lambda<Func<T, object>>(convertedMemberAccess, param);

            if (_useDescOrdering)
            {
                if (cursor.Reverse)
                    return source.AsQueryable().OrderBy(orderPredicate);

                return source.AsQueryable().OrderByDescending(orderPredicate);
            }

            if (cursor.Reverse)
                return source.AsQueryable().OrderByDescending(orderPredicate);

            return source.AsQueryable().OrderBy(orderPredicate);
        }

        private IQueryable<T> ApplyFilterIfRequired<T>(IQueryable<T> source, CursorDetails cursor)
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
                // Which expression should apply given CURSOR vs REVERSED FIELD
                BinaryExpression lessThanOrGreaterThanExpression;
                if (reverse != _useDescOrdering)
                    lessThanOrGreaterThanExpression = Expression.LessThan(convertedMemberAccess, currentPositionExpression);
                else
                    lessThanOrGreaterThanExpression = Expression.GreaterThan(convertedMemberAccess, currentPositionExpression);
                // Final expression
                var filterExpression = Expression.Lambda<Func<T, bool>>(lessThanOrGreaterThanExpression, param);

                return source.Where(filterExpression);
            }

            return source;
        }
    }
}
