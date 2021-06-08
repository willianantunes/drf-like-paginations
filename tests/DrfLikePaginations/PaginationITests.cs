using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DrfLikePaginations;
using FluentAssertions;
using Tests.Support;
using Xunit;

namespace Tests.DrfLikePaginations
{
    public record Person(int Id, string Name);

    public class PaginationITests
    {
        public class Options
        {
            private readonly int _defaultPageLimit;
            private readonly Pagination _pagination;
            private readonly InMemoryDbContextBuilder.TestDbContext<Person> _dbContext;
            private readonly string _url;
            private readonly int _defaultMaxPageLimit;

            public Options()
            {
                _dbContext = InMemoryDbContextBuilder.CreateDbContext<Person>();
                _defaultPageLimit = 10;
                _defaultMaxPageLimit = 25;
                _pagination = new Pagination(_defaultPageLimit, _defaultMaxPageLimit);
                _url = "https://www.willianantunes.com";
            }

            [Fact(DisplayName = "When no options such as limit or offset are provided")]
            public async Task ShouldCreatePaginatedScenarioOptions1()
            {
                // Arrange
                var query = await CreateScenarioWith50People(_dbContext);
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(String.Empty);
                // Act
                var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                // Assert
                paginated.Count.Should().Be(50);
                paginated.Results.Should().HaveCount(_defaultPageLimit);
                paginated.Previous.Should().BeNull();
                var expectedNext = $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit}";
                paginated.Next.Should().Be(expectedNext);
            }

            [Fact(DisplayName = "When either limit or offset receive values different than int")]
            public async Task ShouldCreatePaginatedScenarioOptions2()
            {
                // Arrange
                var query = await CreateScenarioWith50People(_dbContext);
                var queryString = "offset=jafar&limit=aladdin";
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(queryString);
                // Act
                var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                // Assert
                paginated.Count.Should().Be(50);
                paginated.Results.Should().HaveCount(_defaultPageLimit);
                paginated.Previous.Should().BeNull();
                var expectedNext = $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit}";
                paginated.Next.Should().Be(expectedNext);
            }

            [Fact(DisplayName = "When only offset is configured")]
            public async Task ShouldCreatePaginatedScenarioOptions3()
            {
                // Arrange
                var query = await CreateScenarioWith50People(_dbContext);
                var offsetValue = 23;
                var queryString = $"offset={offsetValue}";
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(queryString);
                // Act
                var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                // Assert
                paginated.Count.Should().Be(50);
                paginated.Results.Should().HaveCount(_defaultPageLimit);
                var expectedPrevious = $"{_url}/?limit={_defaultPageLimit}&offset={offsetValue - _defaultPageLimit}";
                paginated.Previous.Should().Be(expectedPrevious);
                var expectedNext = $"{_url}/?limit=10&offset={offsetValue + _defaultPageLimit}";
                paginated.Next.Should().Be(expectedNext);
            }

            [Fact(DisplayName = "When provided limit is higher than what is allowed")]
            public async Task ShouldCreatePaginatedScenarioOptions4()
            {
                // Arrange
                var query = await CreateScenarioWith50People(_dbContext);
                var queryString = "limit=1000";
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(queryString);
                // Act
                var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                // Assert
                paginated.Count.Should().Be(50);
                paginated.Results.Should().HaveCount(_defaultMaxPageLimit);
                paginated.Previous.Should().BeNull();
                var expectedNext = $"{_url}/?limit={_defaultMaxPageLimit}&offset={_defaultMaxPageLimit}";
                paginated.Next.Should().Be(expectedNext);
            }
        }

        public class Navigations
        {
            private readonly int _defaultPageLimit;
            private readonly Pagination _pagination;
            private readonly InMemoryDbContextBuilder.TestDbContext<Person> _dbContext;
            private readonly string _url;
            private readonly int _defaultMaxPageLimit;

            public Navigations()
            {
                _dbContext = InMemoryDbContextBuilder.CreateDbContext<Person>();
                _defaultPageLimit = 10;
                _defaultMaxPageLimit = 25;
                _pagination = new Pagination(_defaultPageLimit, _defaultMaxPageLimit);
                _url = "https://www.willianantunes.com";
            }

            [Fact(DisplayName =
                "When the navigation goes from the beginning to end with no provided options at the start")]
            public async Task ShouldCreatePaginatedScenarioNavigation1()
            {
                // First arrangement
                var query = await CreateScenarioWith50People(_dbContext);
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(String.Empty);
                var shouldGetNextPagination = true;
                var listOfPrevious = new List<string>();
                var listOfNext = new List<string>();
                // Act
                while (shouldGetNextPagination)
                {
                    var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                    paginated.Count.Should().Be(50);
                    paginated.Results.Should().HaveCount(_defaultPageLimit);
                    listOfPrevious.Add(paginated.Previous);
                    listOfNext.Add(paginated.Next);
                    if (paginated.Next is null)
                        shouldGetNextPagination = false;
                    else
                    {
                        var queryStrings = paginated.Next.Split("?")[1];
                        queryParams = Http.RetrieveQueryCollectionFromQueryString(queryStrings);
                    }
                }

                // Assert
                var expectedListOfPrevious = new List<string>
                {
                    null,
                    $"{_url}/?limit={_defaultPageLimit}",
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit}",
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit + (_defaultPageLimit * 1)}",
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit + (_defaultPageLimit * 2)}",
                };
                var expectedListOfNext = new List<string>
                {
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit}",
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit + (_defaultPageLimit * 1)}",
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit + (_defaultPageLimit * 2)}",
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit + (_defaultPageLimit * 3)}",
                    null
                };
                listOfPrevious.Should().Equal(expectedListOfPrevious);
                listOfNext.Should().Equal(expectedListOfNext);
            }

            [Fact(DisplayName = "When the navigation goes from the end to beginning")]
            public async Task ShouldCreatePaginatedScenarioNavigation2()
            {
                // First arrangement
                var query = await CreateScenarioWith50People(_dbContext);
                var queryString = "offset=40&limit=10";
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(queryString);
                var shouldGetPreviousPagination = true;
                var listOfPrevious = new List<string>();
                var listOfNext = new List<string>();
                // Act
                while (shouldGetPreviousPagination)
                {
                    var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                    paginated.Count.Should().Be(50);
                    paginated.Results.Should().HaveCount(_defaultPageLimit);
                    listOfPrevious.Add(paginated.Previous);
                    listOfNext.Add(paginated.Next);
                    if (paginated.Previous is null)
                        shouldGetPreviousPagination = false;
                    else
                    {
                        var queryStrings = paginated.Previous.Split("?")[1];
                        queryParams = Http.RetrieveQueryCollectionFromQueryString(queryStrings);
                    }
                }

                // Assert
                var expectedListOfPrevious = new List<string>
                {
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit + (_defaultPageLimit * 2)}",
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit + (_defaultPageLimit * 1)}",
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit}",
                    $"{_url}/?limit={_defaultPageLimit}",
                    null,
                };
                var expectedListOfNext = new List<string>
                {
                    null,
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit + (_defaultPageLimit * 3)}",
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit + (_defaultPageLimit * 2)}",
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit + (_defaultPageLimit * 1)}",
                    $"{_url}/?limit={_defaultPageLimit}&offset={_defaultPageLimit}",
                };
                listOfPrevious.Should().Equal(expectedListOfPrevious);
                listOfNext.Should().Equal(expectedListOfNext);
            }
        }

        private static async Task<IQueryable<Person>> CreateScenarioWith50People(InMemoryDbContextBuilder.TestDbContext<Person> dbContext)
        {
            var persons = new List<Person>();

            foreach (int index in Enumerable.Range(1, 50))
            {
                var person = new Person(index, $"Person {index}");
                persons.Add(person);
            }

            await dbContext.AddRangeAsync(persons);
            await dbContext.SaveChangesAsync();

            return dbContext.Entities.AsQueryable();
        }
    }
}
