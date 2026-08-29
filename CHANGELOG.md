# Changelog

All notable changes to this project are documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Clean Architecture solution layout with Domain, Application, Infrastructure and Api layers
- Hand-rolled CQRS mediator with validation and logging pipeline behaviors
- Result pattern with `Validation`, `NotFound`, `Conflict` and `Forbidden` error types
- Vehicle aggregate with `Create` and `List` use cases
- PostgreSQL persistence via EF Core 10 with an initial migration
- .NET Aspire orchestration for PostgreSQL, Redis and Keycloak with realm import
- Keycloak authentication with the realm roles `admin`, `disponent`, `fahrer` and `viewer`
- Angular 22 frontend with Material, OIDC login and vehicle list
- Vue 3 frontend with Vuetify, OIDC login and vehicle list
- Integration tests using Testcontainers and a test authentication scheme, using AwesomeAssertions rather than FluentAssertions (FluentAssertions became proprietary from version 8)
- CI workflow building the solution, running .NET tests and building both frontends
- `Driver` aggregate end to end (domain, `Create`/`Update`/`Delete`/`GetById`/`List` use cases, API endpoints, and both frontends' driver list and form)
- `Vehicle` CRUD completed with `Update`, `Delete` and `GetById`, alongside the existing `Create`/`List`
- List filters on both aggregates: `status` and `type` for vehicles, `status` for drivers, in addition to `page`/`pageSize`
- Redis caching for list and get-by-id reads on both aggregates, invalidated by key prefix on every write; caching is disabled entirely (writes are skipped, not merely a slower path) when no Redis connection string is configured, because caching without the ability to invalidate is a correctness hazard, not a performance win
- Per-field validation errors: FluentValidation failures are now grouped under their real field name in the API's `ValidationProblem` response, letting both frontends' forms bind a server-side error onto the matching field instead of only showing a form-level message
- A fallback authorization policy requiring an authenticated user on every endpoint, so an endpoint that forgets `RequireAuthorization` fails closed instead of silently being public; the trade-off, recorded in the spec (§9), is that an unmatched route now answers `401` rather than `404` for an unauthenticated caller
- Application-layer line-coverage gate in CI, failing the backend job when the `TransBrain.Application.Tests` suite's line coverage drops below the spec's 80% floor (measured at 86.9% line / 84.7% branch when the gate was added)
- German-language operator guides for both frontends (`docs/BEDIENUNG_TRANSBRAIN_WEB.md`, `docs/BEDIENUNG_TRANSBRAIN_VUEWEB.md`)

### Changed
- The API's response language (FluentValidation's validation messages) is now pinned to English (invariant culture) at startup, rather than following the host machine's ambient culture; see README.md's "API response language" section for the two lines to change together to switch to German instead
