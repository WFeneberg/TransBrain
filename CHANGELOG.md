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
- `Address`, `Cargo` and `TimeWindow` value objects in `TransBrain.Domain/Common`, shared by the order aggregate and reusable by later ones
- `TransportOrder` aggregate end to end (domain, `Create`/`Update`/`Cancel`/`GetById`/`List` use cases, API endpoints, and both frontends' order list and form)
- Collision-free order numbers in the form `TB-2027-00042`, handed out by an atomic per-year database counter (`INSERT ... ON CONFLICT DO UPDATE ... RETURNING`) rather than a `SELECT MAX(...) + 1`, so two concurrent creates cannot receive the same number; a unique index on the column stays as a second line of defence
- Order status machine (`Draft` -> `Planned` -> `InTransit` -> `Delivered`, with `Cancel` allowed only from `Draft` or `Planned`), owned entirely by the entity and returning a `Conflict` for a refused transition rather than silently declining. Only `Cancel` has an endpoint in this phase; the other transitions are driven by tour operations in a later one
- Order list filters: `status`, `pickupFrom` and `pickupTo`, in addition to `page`/`pageSize`
- `Tour` aggregate end to end (domain, `Create`/`AssignOrder`/`RemoveOrder`/`Start`/`Complete`/`GetById`/`List` use cases, API endpoints, and both frontends' tour list, form and detail screens)
- Tour capacity invariants: the sum of the assigned orders' weight and load meters must fit the vehicle, checked against the whole tour rather than the incoming order alone
- Tour assignment invariants: the vehicle must be `Available`, the driver `Available`, and the driver's licence valid on the tour date (a licence expiring exactly on that date still counts)
- Double-booking protection: unique indexes on `(tour_date, vehicle_id)` and `(tour_date, driver_id)` make a second tour for the same vehicle or driver on the same day impossible, including under concurrent requests — a pre-flight query could not
- Stops are derived, not entered: assigning an order creates exactly one `Pickup` and one `Delivery` stop, the pickup always first; removing it drops both and renumbers the rest contiguously
- The order status machine is now driven end to end by tour operations, as spec §5.4 draws it: assigning plans an order, starting a tour puts every assigned order `InTransit`, completing it marks them `Delivered`
- `TransportOrder.ReturnToDraft`, so an order removed from a tour becomes assignable again instead of being stranded in `Planned` — a transition the spec's diagram omits but its `RemoveOrder` use case requires
- `ICurrentUser` abstraction and driver-scoped authorization: a `fahrer` sees only their own tours and may start and complete only those, enforced in the handlers because a policy cannot see which tour a request addresses

### Changed
- Both frontends' list screens and pickers now request the API's maximum page size instead of the default 20. Lists come back sorted ascending, so one default page showed the twenty *oldest* records and hid everything added since — with no pagination controls to reach the rest. This is a stopgap; real paging is still needed above 100 rows
- Vuetify data tables in the Vue frontend no longer paginate a second time on top of the server's page, which could hide a record the server had returned
- The vehicle and driver forms now hydrate the OIDC session before their first request, so a form opened by direct navigation (a bookmark, a page reload) no longer answers `401` to a signed-in user — the same fix the order form received earlier
- The API's response language (FluentValidation's validation messages) is now pinned to English (invariant culture) at startup, rather than following the host machine's ambient culture; see README.md's "API response language" section for the two lines to change together to switch to German instead
