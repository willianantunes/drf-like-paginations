# DRF Like Paginations

[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=willianantunes_drf-like-paginations&metric=coverage)](https://sonarcloud.io/dashboard?id=willianantunes_drf-like-paginations)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=willianantunes_drf-like-paginations&metric=ncloc)](https://sonarcloud.io/dashboard?id=willianantunes_drf-like-paginations)

This project is an attempt to mimic [LimitOffsetPagination](https://www.django-rest-framework.org/api-guide/pagination/#limitoffsetpagination) that is available on DRF. Many other types of paginations can be incorporated beyond the ones available [here](https://www.django-rest-framework.org/api-guide/pagination/#api-reference). This is just a start.

    dotnet add package DrfLikePaginations

It supports **queries in your data** given what is informed through the URL as query strings. You can get some details about how it works if you look at the tests in [PaginationITests.Queries](https://github.com/willianantunes/drf-like-paginations/blob/abdce3ab9293af95d923cf0b25634f555fad4aaa/tests/DrfLikePaginations/PaginationITests.cs#L218) class.

It also support **model transformation**. If you don't want to expose your model, you can create a DTO and then provide a function which transforms your data. Check out one example on [this integration test](https://github.com/willianantunes/drf-like-paginations/blob/67c5a14babcbfbd75ded8d603a9575a1bb15511a/tests/DrfLikePaginations/PaginationITests.cs#L354-L372).

The following project is using it and you can use as an example to set up yours:

- [Tic Tac Toe C# Playground](https://github.com/willianantunes/tic-tac-toe-csharp-playground)

## Sample output

You can check out one sample output below from the project [ef-core-how-to-handle-migrations-in-production](https://github.com/willianantunes/tutorials/tree/d19609a50605a8d519ade3a568995da66568b212/2021/XX/ef-core-how-to-handle-migrations-in-production):

```json
{
  "count": 100,
  "next": "http://0.0.0.0:8000/api/v1/todoitems?limit=5&offset=5",
  "previous": null,
  "results": [
    {
      "name": "TD 1",
      "isComplete": false,
      "id": 1,
      "createdAt": "2021-06-08T19:00:17.873423",
      "updatedAt": "2021-06-08T19:00:17.87347"
    },
    {
      "name": "TD 73",
      "isComplete": false,
      "id": 2,
      "createdAt": "2021-06-08T19:00:17.873571",
      "updatedAt": "2021-06-08T19:00:17.873571"
    },
    {
      "name": "TD 72",
      "isComplete": true,
      "id": 3,
      "createdAt": "2021-06-08T19:00:17.87357",
      "updatedAt": "2021-06-08T19:00:17.87357"
    },
    {
      "name": "TD 71",
      "isComplete": false,
      "id": 4,
      "createdAt": "2021-06-08T19:00:17.87357",
      "updatedAt": "2021-06-08T19:00:17.87357"
    },
    {
      "name": "TD 70",
      "isComplete": true,
      "id": 5,
      "createdAt": "2021-06-08T19:00:17.873569",
      "updatedAt": "2021-06-08T19:00:17.873569"
    }
  ]
}
```

## How to use it

You can add the following in your `appsettings.json`:

```json
{
  "Pagination": {
    "Size": 5
  }
}
```

Then configure the pagination service like the following:

```csharp
var paginationSize = int.Parse(Configuration["Pagination:Size"]);
services.AddSingleton<IPagination>(new Pagination(paginationSize));
```

Now you are able to use it 😍! One full example:

```csharp
namespace EFCoreHandlingMigrations.Controllers.V1
{
    [ApiController]
    [Route("api/v1/[controller]")]
    public class TodoItemsController : ControllerBase
    {
        private readonly DbSet<TodoItem> _databaseSet;
        private readonly IPagination _pagination;

        public TodoItemsController(AppDbContext context, IPagination pagination)
        {
            _databaseSet = context.TodoItems;
            _pagination = pagination;
        }

        [HttpGet]
        public async Task<Paginated<TodoItem>> GetTodoItems()
        {
            var query = _databaseSet.AsNoTracking().OrderBy(t => t.CreatedAt);
            var displayUrl = Request.GetDisplayUrl();
            var queryParams = Request.Query;

            return await _pagination.CreateAsync(query, displayUrl, queryParams);
        }
    }
}
```

## Compose services

If you look at [docker-compose.yaml](https://github.com/willianantunes/drf-like-paginations/blob/abdce3ab9293af95d923cf0b25634f555fad4aaa/docker-compose.yaml#L7-L30), you'll find three main services:

- [tests](https://github.com/willianantunes/drf-like-paginations/blob/fff46e8627c1bfd23fcc2ef7fe9e8663e6e87156/docker-compose.yaml#L7): run all the tests, and generate tests-reports folder with coverage data.
- [lint](https://github.com/willianantunes/drf-like-paginations/blob/fff46e8627c1bfd23fcc2ef7fe9e8663e6e87156/docker-compose.yaml#L15): check if the project is valid given standard dotnet-format rules
- [formatter](https://github.com/willianantunes/drf-like-paginations/blob/fff46e8627c1bfd23fcc2ef7fe9e8663e6e87156/docker-compose.yaml#L23): format the project given standard dotnet-format rules
