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
            private IPagination _pagination;
            private readonly InMemoryDbContextBuilder.TestDbContext<Situation> _dbContext;
            private readonly string _url;
            private readonly int _defaultMaxPageLimit;

            public Options()
            {
                _dbContext = InMemoryDbContextBuilder.CreateDbContext<Situation>();
                _defaultPageLimit = 10;
                _defaultMaxPageLimit = 25;
                _url = "https://www.willianantunes.com";
            }

            [Fact(DisplayName = "When no options are provided ASC ORDERING")]
            public async Task ShouldCreatePaginatedScenarioOptions1()
            {
                // Arrange
                _pagination = new CursorPagination(_defaultPageLimit, _defaultMaxPageLimit);
                var query = await CreateScenarioWith50Situations(_dbContext);
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(String.Empty);
                // Act
                var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                // Assert
                paginated.Count.Should().BeNull();
                paginated.Results.Should().HaveCount(_defaultPageLimit);
                paginated.Previous.Should().BeNull();
                var allRetrievedIds = paginated.Results.Select(v => v.Id).ToList();
                var expectedRetrievedIds = new List<int> {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};
                allRetrievedIds.Should().Equal(expectedRetrievedIds);
                var paginatedNext = paginated.Next;
                paginatedNext.Should().StartWith("https://www.willianantunes.com/?");
                var paginationSetup = BuildPaginationSetup(paginated.Next)!;
                paginationSetup.Reverse.Should().BeFalse();
                int.Parse(paginationSetup.Position!).Should().Be(10);
                int.Parse(paginationSetup.Limit!).Should().Be(_defaultPageLimit);
            }
            
            [Fact(DisplayName = "When no options are provided DESC ORDERING")]
            public async Task ShouldCreatePaginatedScenarioOptions2()
            {
                // Arrange
                _pagination = new CursorPagination(_defaultPageLimit, _defaultMaxPageLimit, "-Id");
                var query = await CreateScenarioWith50Situations(_dbContext);
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(String.Empty);
                // Act
                var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                // Assert
                paginated.Count.Should().BeNull();
                paginated.Results.Should().HaveCount(_defaultPageLimit);
                paginated.Previous.Should().BeNull();
                var allRetrievedIds = paginated.Results.Select(v => v.Id).ToList();
                var expectedRetrievedIds = new List<int> {50, 49, 48, 47, 46, 45, 44, 43, 42, 41};
                allRetrievedIds.Should().Equal(expectedRetrievedIds);
                var paginatedNext = paginated.Next;
                paginatedNext.Should().StartWith("https://www.willianantunes.com/?");
                var paginationSetup = BuildPaginationSetup(paginated.Next)!;
                paginationSetup.Reverse.Should().BeFalse();
                int.Parse(paginationSetup.Position!).Should().Be(41);
                int.Parse(paginationSetup.Limit!).Should().Be(_defaultPageLimit);
            }
            
            [Fact(DisplayName = "Should throw exception when field does not match pattern")]
            public void ShouldCreatePaginatedScenarioOptions3()
            {
                // Arrange
                var invalidField = "-1Id";
                var pattern = @"^-?([a-zA-Z]+)$";
                // Act
                Action act = () => new CursorPagination(_defaultPageLimit, _defaultMaxPageLimit, invalidField);;
                // Assert
                var expectedMessage = $"The field {invalidField} does not match the pattern: {pattern}";
                act.Should().Throw<ProvidedFieldForOrderingIsWrongException>()
                    .WithMessage(expectedMessage);
            }            
        }

        public class Navigations
        {
            private readonly int _defaultPageLimit;
            private IPagination _pagination;
            private readonly InMemoryDbContextBuilder.TestDbContext<Situation> _dbContext;
            private readonly string _url;
            private readonly int _defaultMaxPageLimit;

            public Navigations()
            {
                _dbContext = InMemoryDbContextBuilder.CreateDbContext<Situation>();
                _defaultPageLimit = 10;
                _defaultMaxPageLimit = 25;
                _url = "https://www.willianantunes.com";
            }

            [Fact(DisplayName = "When the navigation goes from the beginning to end ASC ORDERING")]
            public async Task ShouldCreatePaginatedScenarioNavigation1()
            {
                // First arrangement
                _pagination = new CursorPagination(_defaultPageLimit, _defaultMaxPageLimit);
                var query = await CreateScenarioWith50Situations(_dbContext);
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(String.Empty);
                var shouldGetNextPagination = true;
                var listOfResults = new List<List<int>>();
                var listOfPrevious = new List<PaginationSetup?>();
                var listOfNext = new List<PaginationSetup?>();
                // Act
                while (shouldGetNextPagination)
                {
                    var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                    paginated.Count.Should().BeNull();
                    paginated.Results.Should().HaveCount(_defaultPageLimit);
                    var allRetrievedIds = paginated.Results.Select(v => v.Id).ToList();
                    listOfResults.Add(allRetrievedIds);
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
                    new(true, "11", "10"),
                    new(true, "21", "10"),
                    new(true, "31", "10"),
                    new(true, "41", "10"),
                };
                var expectedListOfNext = new List<PaginationSetup?>
                {
                    new(false, "10", "10"),
                    new(false, "20", "10"),
                    new(false, "30", "10"),
                    new(false, "40", "10"),
                    null
                };
                var expectedListOfResults = new List<List<int>>
                {
                    new() {1, 2, 3, 4, 5, 6, 7, 8, 9, 10},
                    new() {11, 12, 13, 14, 15, 16, 17, 18, 19, 20},
                    new() {21, 22, 23, 24, 25, 26, 27, 28, 29, 30},
                    new() {31, 32, 33, 34, 35, 36, 37, 38, 39, 40},
                    new() {41, 42, 43, 44, 45, 46, 47, 48, 49, 50},
                };
                listOfPrevious.Should().Equal(expectedListOfPrevious);
                listOfNext.Should().Equal(expectedListOfNext);
                listOfResults.Should().HaveCount(5);
                foreach (var (result, index) in listOfResults.Select((item, index) => (item, index)))
                    result.Should().Equal(expectedListOfResults[index]);
            }
            
            [Fact(DisplayName = "When the navigation goes from the beginning to end DESC ORDERING")]
            public async Task ShouldCreatePaginatedScenarioNavigation2()
            {
                // First arrangement
                _pagination = new CursorPagination(_defaultPageLimit, _defaultMaxPageLimit, "-Id");
                var query = await CreateScenarioWith50Situations(_dbContext);
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(String.Empty);
                var shouldGetNextPagination = true;
                var listOfResults = new List<List<int>>();
                var listOfPrevious = new List<PaginationSetup?>();
                var listOfNext = new List<PaginationSetup?>();
                // Act
                while (shouldGetNextPagination)
                {
                    var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                    paginated.Count.Should().BeNull();
                    paginated.Results.Should().HaveCount(_defaultPageLimit);
                    var allRetrievedIds = paginated.Results.Select(v => v.Id).ToList();
                    listOfResults.Add(allRetrievedIds);
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
                    new(true, "40", "10"),
                    new(true, "30", "10"),
                    new(true, "20", "10"),
                    new(true, "10", "10"),
                };
                var expectedListOfNext = new List<PaginationSetup?>
                {
                    new(false, "41", "10"),
                    new(false, "31", "10"),
                    new(false, "21", "10"),
                    new(false, "11", "10"),
                    null
                };
                var expectedListOfResults = new List<List<int>>
                {
                    new() {50, 49, 48, 47, 46, 45, 44, 43, 42, 41},
                    new() {40, 39, 38, 37, 36, 35, 34, 33, 32, 31},
                    new() {30, 29, 28, 27, 26, 25, 24, 23, 22, 21},
                    new() {20, 19, 18, 17, 16, 15, 14, 13, 12, 11},
                    new() {10, 9, 8, 7, 6, 5, 4, 3, 2, 1},
                };
                listOfPrevious.Should().Equal(expectedListOfPrevious);
                listOfNext.Should().Equal(expectedListOfNext);
                listOfResults.Should().HaveCount(5);
                foreach (var (result, index) in listOfResults.Select((item, index) => (item, index)))
                    result.Should().Equal(expectedListOfResults[index]);
            }            

            [Fact(DisplayName = "When the navigation goes from the end to beginning ASC ORDERING")]
            public async Task ShouldCreatePaginatedScenarioNavigation3()
            {
                // First arrangement
                _pagination = new CursorPagination(_defaultPageLimit, _defaultMaxPageLimit);
                var query = await CreateScenarioWith50Situations(_dbContext);
                var cursorQueryString = CreateCursorQueryString(true, "51");
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(cursorQueryString);
                var shouldGetPreviousPagination = true;
                var listOfResults = new List<List<int>>();
                var listOfPrevious = new List<PaginationSetup?>();
                var listOfNext = new List<PaginationSetup?>();
                // Act
                while (shouldGetPreviousPagination)
                {
                    var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                    paginated.Count.Should().BeNull();
                    paginated.Results.Should().HaveCount(_defaultPageLimit);
                    var allRetrievedIds = paginated.Results.Select(v => v.Id).ToList();
                    listOfResults.Add(allRetrievedIds);
                    listOfPrevious.Add(BuildPaginationSetup(paginated.Previous));
                    listOfNext.Add(BuildPaginationSetup(paginated.Next));
                    if (paginated.Previous is null)
                        shouldGetPreviousPagination = false;
                    else
                    {
                        var queryStrings = paginated.Previous.Split("?")[1];
                        queryParams = Http.RetrieveQueryCollectionFromQueryString(queryStrings);
                    }
                }
                // Assert
                var expectedListOfPrevious = new List<PaginationSetup?>
                {
                    new(true, "41", "10"),
                    new(true, "31", "10"),
                    new(true, "21", "10"),
                    new(true, "11", "10"),
                    null,
                };
                var expectedListOfNext = new List<PaginationSetup?>
                {
                    new(false, "50", "10"),
                    new(false, "40", "10"),
                    new(false, "30", "10"),
                    new(false, "20", "10"),
                    new(false, "10", "10"),
                };
                var expectedListOfResults = new List<List<int>>
                {
                    new() {41, 42, 43, 44, 45, 46, 47, 48, 49, 50},
                    new() {31, 32, 33, 34, 35, 36, 37, 38, 39, 40},
                    new() {21, 22, 23, 24, 25, 26, 27, 28, 29, 30},
                    new() {11, 12, 13, 14, 15, 16, 17, 18, 19, 20},
                    new() {1, 2, 3, 4, 5, 6, 7, 8, 9, 10},
                };
                listOfPrevious.Should().Equal(expectedListOfPrevious);
                listOfNext.Should().Equal(expectedListOfNext);
                foreach (var (result, index) in listOfResults.Select((item, index) => (item, index)))
                    result.Should().Equal(expectedListOfResults[index]);
            }
            
            [Fact(DisplayName = "When the navigation goes from the end to beginning DESC ORDERING")]
            public async Task ShouldCreatePaginatedScenarioNavigation4()
            {
                // First arrangement
                _pagination = new CursorPagination(_defaultPageLimit, _defaultMaxPageLimit, "-Id");
                var query = await CreateScenarioWith50Situations(_dbContext);
                var cursorQueryString = CreateCursorQueryString(true, "0");
                var queryParams = Http.RetrieveQueryCollectionFromQueryString(cursorQueryString);
                var shouldGetPreviousPagination = true;
                var listOfResults = new List<List<int>>();
                var listOfPrevious = new List<PaginationSetup?>();
                var listOfNext = new List<PaginationSetup?>();
                // Act
                while (shouldGetPreviousPagination)
                {
                    var paginated = await _pagination.CreateAsync(query, _url, queryParams);
                    paginated.Count.Should().BeNull();
                    paginated.Results.Should().HaveCount(_defaultPageLimit);
                    var allRetrievedIds = paginated.Results.Select(v => v.Id).ToList();
                    listOfResults.Add(allRetrievedIds);
                    listOfPrevious.Add(BuildPaginationSetup(paginated.Previous));
                    listOfNext.Add(BuildPaginationSetup(paginated.Next));
                    if (paginated.Previous is null)
                        shouldGetPreviousPagination = false;
                    else
                    {
                        var queryStrings = paginated.Previous.Split("?")[1];
                        queryParams = Http.RetrieveQueryCollectionFromQueryString(queryStrings);
                    }
                }
                // Assert
                var expectedListOfPrevious = new List<PaginationSetup?>
                {
                    new(true, "10", "10"),
                    new(true, "20", "10"),
                    new(true, "30", "10"),
                    new(true, "40", "10"),
                    null,
                };
                var expectedListOfNext = new List<PaginationSetup?>
                {
                    new(false, "1", "10"),
                    new(false, "11", "10"),
                    new(false, "21", "10"),
                    new(false, "31", "10"),
                    new(false, "41", "10"),
                };
                var expectedListOfResults = new List<List<int>>
                {
                    new() {10, 9, 8, 7, 6, 5, 4, 3, 2, 1},
                    new() {20, 19, 18, 17, 16, 15, 14, 13, 12, 11},
                    new() {30, 29, 28, 27, 26, 25, 24, 23, 22, 21},
                    new() {40, 39, 38, 37, 36, 35, 34, 33, 32, 31},
                    new() {50, 49, 48, 47, 46, 45, 44, 43, 42, 41},
                };
                listOfPrevious.Should().Equal(expectedListOfPrevious);
                listOfNext.Should().Equal(expectedListOfNext);
                foreach (var (result, index) in listOfResults.Select((item, index) => (item, index)))
                    result.Should().Equal(expectedListOfResults[index]);
            }
        }

        private static string CreateCursorQueryString(bool reverse, string position)
        {
            var uriBuilderForCursorOnly = new UriBuilder();
            var cursorDetailsParams = HttpUtility.ParseQueryString(uriBuilderForCursorOnly.Query);
            cursorDetailsParams["r"] = reverse.ToString();
            cursorDetailsParams["p"] = position;
            uriBuilderForCursorOnly.Query = cursorDetailsParams.ToString();
            var cursorQueryString = uriBuilderForCursorOnly.Uri.PathAndQuery;
            var cleanedCursorQueryString = String.Join("", cursorQueryString.Split("?")[1]);
            var encodedCursorQueryString = Base64.Encode(cleanedCursorQueryString);

            return $"cursor={encodedCursorQueryString}";
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
