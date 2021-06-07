# DRF Like Paginations

[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=willianantunes_drf-like-paginations&metric=coverage)](https://sonarcloud.io/dashboard?id=willianantunes_drf-like-paginations)
[![Lines of Code](https://sonarcloud.io/api/project_badges/measure?project=willianantunes_drf-like-paginations&metric=ncloc)](https://sonarcloud.io/dashboard?id=willianantunes_drf-like-paginations)

This project is an attempt to mimic [LimitOffsetPagination](https://www.django-rest-framework.org/api-guide/pagination/#limitoffsetpagination) that is available on DRF. Many other types of paginations can be incorporated beyond the ones available [here](https://www.django-rest-framework.org/api-guide/pagination/#api-reference). This is just a start.

## Compose services

If you look at [docker-compose.yaml](./docker-compose.yaml), you'll find three services:

- [tests](https://github.com/willianantunes/drf-like-paginations/blob/fff46e8627c1bfd23fcc2ef7fe9e8663e6e87156/docker-compose.yaml#L7): run all the tests, and generate tests-reports folder with coverage data.
- [lint](https://github.com/willianantunes/drf-like-paginations/blob/fff46e8627c1bfd23fcc2ef7fe9e8663e6e87156/docker-compose.yaml#L15): check if the project is valid given standard dotnet-format rules
- [formatter](https://github.com/willianantunes/drf-like-paginations/blob/fff46e8627c1bfd23fcc2ef7fe9e8663e6e87156/docker-compose.yaml#L23): format the project given standard dotnet-format rules
