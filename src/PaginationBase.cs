using System;
using System.Linq;
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
    }
}
