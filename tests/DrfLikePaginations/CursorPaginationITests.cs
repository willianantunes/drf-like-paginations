using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using DrfLikePaginations;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tests.Support;
using Xunit;

namespace Tests.DrfLikePaginations
{
    record PaginationSetup(bool Reverse, string? Position, string? Limit);
    record Situation(int Id, string Name, DateTime CreatedAt);

    public class CursorPaginationITests
    {
        public class Options
        {
            private readonly int _defaultPageLimit;
            private readonly IPagination _pagination;
            private readonly InMemoryDbContextBuilder.TestDbContext<Situation> _dbContext;
            private readonly string _url;
            private readonly int _defaultMaxPageLimit;

            public Options()
            {
                _dbContext = InMemoryDbContextBuilder.CreateDbContext<Situation>();
                _defaultPageLimit = 10;
                _defaultMaxPageLimit = 25;
                _pagination = new CursorPagination(_defaultPageLimit, _defaultMaxPageLimit);
                _url = "https://www.willianantunes.com";
            }

            [Fact(DisplayName = "When no options are provided")]
            public async Task ShouldCreatePaginatedScenarioOptions1()
            {
                // Arrange
                var query = await CreateScenarioWith50Situations(_dbContext);
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(String.Empty);
                // Act
                var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                // Assert
                paginated.Count.Should().BeNull();
                paginated.Results.Should().HaveCount(_defaultPageLimit);
                paginated.Previous.Should().BeNull();
                var paginatedNext = paginated.Next;
                paginatedNext.Should().StartWith("https://www.willianantunes.com/?");
                var paginationSetup = BuildPaginationSetup(paginated.Next)!;
                paginationSetup.Reverse.Should().BeFalse();
                int.Parse(paginationSetup.Position!).Should().Be(10);
                int.Parse(paginationSetup.Limit!).Should().Be(_defaultPageLimit);
            }
        }

        public class Navigations
        {
            private readonly int _defaultPageLimit;
            private readonly IPagination _pagination;
            private readonly InMemoryDbContextBuilder.TestDbContext<Situation> _dbContext;
            private readonly string _url;
            private readonly int _defaultMaxPageLimit;

            public Navigations()
            {
                _dbContext = InMemoryDbContextBuilder.CreateDbContext<Situation>();
                _defaultPageLimit = 10;
                _defaultMaxPageLimit = 25;
                _pagination = new CursorPagination(_defaultPageLimit, _defaultMaxPageLimit);
                _url = "https://www.willianantunes.com";
            }

            [Fact(DisplayName = "When the navigation goes from the beginning to end with no provided options at the start")]
            public async Task ShouldCreatePaginatedScenarioNavigation1()
            {
                // First arrangement
                var query = await CreateScenarioWith50Situations(_dbContext);
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(String.Empty);
                var shouldGetNextPagination = true;
                var listOfPrevious = new List<PaginationSetup?>();
                var listOfNext = new List<PaginationSetup?>();
                // Act
                while (shouldGetNextPagination)
                {
                    var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                    paginated.Count.Should().BeNull();
                    paginated.Results.Should().HaveCount(_defaultPageLimit);
                    listOfPrevious.Add(BuildPaginationSetup(paginated.Previous));
                    listOfNext.Add(BuildPaginationSetup(paginated.Next));
                    if (paginated.Next is null)
                        shouldGetNextPagination = false;
                    else
                    {
                        var queryStrings = paginated.Next.Split("?")[1];
                        queryParams = Http.RetrieveQueryCollectionFromQueryString(queryStrings);
                    }
                }
                // Assert
                var expectedListOfPrevious = new List<PaginationSetup?>
                {
                    null,
                    new (true, "10", "10"),
                    new (true, "20", "10"),
                    new (true, "30", "10"),
                    new (true, "40", "10"),
                };
                var expectedListOfNext = new List<PaginationSetup?>
                {
                    new (false, "10", "10"),
                    new (false, "20", "10"),
                    new (false, "30", "10"),
                    new (false, "40", "10"),
                    null
                };
                listOfPrevious.Should().Equal(expectedListOfPrevious);
                listOfNext.Should().Equal(expectedListOfNext);
            }
        }
        
        private static PaginationSetup? BuildPaginationSetup(string? uri)
        {
            if (uri is not null)
            {
                var builderUri = new UriBuilder(uri);
                var nameValueCollection = HttpUtility.ParseQueryString(builderUri.Query);
                nameValueCollection.Keys.Should().HaveCount(2);
                // Cursor validation
                var cursorValue = Base64.Decode(nameValueCollection["cursor"]!);
                var limitValue = nameValueCollection["limit"];
                var cursorQueryStrings = Http.RetrieveQueryCollectionFromQueryString(cursorValue);
                cursorQueryStrings.Keys.Should().HaveCount(2);
                var reverse = bool.Parse(cursorQueryStrings["r"]);
                var position = cursorQueryStrings["p"].ToString();

                return new PaginationSetup(reverse, position, limitValue);
            }

            return null;
        }

        private static async Task<IQueryable<Situation>> CreateScenarioWith50Situations(InMemoryDbContextBuilder.TestDbContext<Situation> dbContext)
        {
            var situations = new List<Situation>();

            foreach (int index in Enumerable.Range(1, 50))
            {
                var situation = new Situation(index, $"Situation {index}", DateTime.Now);
                situations.Add(situation);
            }

            await dbContext.AddRangeAsync(situations);
            await dbContext.SaveChangesAsync();

            // https://docs.microsoft.com/en-us/ef/core/querying/tracking#no-tracking-queries
            return dbContext.Entities.AsNoTracking();
        }
    }
}
