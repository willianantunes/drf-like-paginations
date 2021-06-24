using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace DrfLikePaginations
{
    public interface IPagination
    {
        public Task<Paginated<T>> CreateAsync<T>(IQueryable<T> source, string url, IQueryCollection queryParams);
        public Task<Paginated<TResult>> CreateAsync<T, TResult>(IQueryable<T> source, string url, IQueryCollection queryParams, Func<T, TResult> transform);
    }
}
