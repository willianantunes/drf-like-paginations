using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace DrfLikePaginations
{
    public abstract class PaginationBase : IPagination
    {
        protected readonly int _defaultLimit;
        protected readonly int _maxPageSize;
        protected const string _limitQueryParam = "limit";

        protected PaginationBase(int defaultPageSize, int maxPageSize)
        {
            _defaultLimit = defaultPageSize;
            _maxPageSize = maxPageSize;
        }

        public abstract Task<Paginated<T>> CreateAsync<T>(IQueryable<T> source, string url, IQueryCollection queryParams);
        public abstract Task<Paginated<TResult>> CreateAsync<T, TResult>(IQueryable<T> source, string url, IQueryCollection queryParams, Func<T, TResult> transform);

        protected int RetrieveConfiguredLimit(StringValues values)
        {
            var value = values.FirstOrDefault();

            if (value is not null)
            {
                int requestedLimitValue;
                var couldBeParsed = int.TryParse(value, out requestedLimitValue);

                if (couldBeParsed && requestedLimitValue > 0)
                {
                    var valueToBeReturned = requestedLimitValue > _maxPageSize ? _maxPageSize : requestedLimitValue;
                    return valueToBeReturned;
                }
            }

            return _defaultLimit;
        }

        protected IQueryable<T> ApplyCustomFilterIfApplicable<T>(IQueryable<T> source, IEnumerable<KeyValuePair<string, StringValues>> queryParams)
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
