# TransBrain

Dispatch software for a trucking company: vehicles, drivers, transport orders and tours,
built with .NET 11 (Clean Architecture, a hand-rolled CQRS mediator, EF Core 10 /
PostgreSQL, Redis) behind Keycloak-issued OIDC tokens, with two frontends — Angular and
Vue — talking to the same API.

Phase 1 (foundation and walking skeleton) delivered a `Vehicle` aggregate with `Create`
and `List`, wired end to end through both frontends. Phase 2 (master data completion)
added a full `Driver` aggregate, completed vehicle CRUD (`Update`, `Delete`, `GetById`),
added list filters, Redis caching, per-field validation errors and a fallback
authorization policy. See [CHANGELOG.md](CHANGELOG.md) for the detailed list of what
exists today, and `.superpowers/sdd/` for the phase plans.

## Documentation

- [docs/DEVELOPER_GUIDE.md](docs/DEVELOPER_GUIDE.md) — architecture, conventions, how to extend and test
- [docs/INSTALLATION.md](docs/INSTALLATION.md) — local (Aspire), Docker Compose and Azure (Bicep)
- [docs/KEYCLOAK.md](docs/KEYCLOAK.md) — authentication setup

## Prerequisites

- .NET 11 SDK
- Node.js >= 26.4.0
- Docker (Docker Desktop, or an equivalent engine) — required both to run the stack
  (Postgres, Redis, Keycloak all run as containers) and to run the API integration tests,
  which are backed by Testcontainers
- A trusted local HTTPS development certificate — see below, this is not optional

### Trust the development HTTPS certificate (one-time)

Keycloak's authority is `https://localhost:8080/realms/transbrain` — HTTPS, using the
ASP.NET Core/Aspire self-signed development certificate. Both the API (which calls
Keycloak's OIDC discovery endpoint over the backchannel) and any browser you log in with
need to trust that certificate. Run this once per machine:

```bash
dotnet dev-certs https --trust
```

If you skip this, OIDC discovery fails before the login screen ever appears, and the
resulting error will not look like a certificate problem — it looks like the API or
Keycloak is misconfigured or unreachable. If login is failing and nothing else obviously
explains it, this is the first thing to check.

## Running the stack

```bash
dotnet run --project src/TransBrain.AppHost
```

This is the reliable way to start everything (API, Postgres, Redis, Keycloak with the
realm imported, and both frontends) via .NET Aspire, and every successful verification
during this project's development used this command. It prints a link to the Aspire
dashboard, which lists the actual ports each resource bound to.

**Known issue — `aspire run` is unreliable in this environment.** The Aspire CLI's
`aspire run` timed out twice during this project ("Timed out waiting for AppHost to
start", once at 120s and once at 420s) and force-killed an orchestration that was
actually succeeding underneath, orphaning containers — after one such timeout, 8
container sets were left behind in state `Exited (143)`. Prefer `dotnet run` above. If
you do use `aspire run` and it times out, check `docker ps -a` for orphaned containers
from the run before starting again — do not just retry on top of them.

### What survives a restart

Postgres has a persistent data volume: data survives a restart, and EF Core applies only
pending migrations on start. To start from an empty database, stop the AppHost and remove
the volume (`docker volume ls | grep postgres`, then `docker volume rm <name>`). The volume
is tied to the generated password in the AppHost's user secrets
(`Parameters:postgres-password`); if those are cleared, Postgres rejects the connection and
the API hangs in `Waiting` — remove the volume in that case.

Keycloak has deliberately no data volume: it re-imports
`src/TransBrain.AppHost/realms/transbrain-realm.json` on every start, so that file stays the
authoritative source of realm configuration. Realm edits made through the Keycloak admin
console are gone after a restart.

### Database migrations are only applied automatically in Development

`Program.cs` calls `Database.MigrateAsync()` only when `IsDevelopment()` is true. Any other
environment (Staging, QA, production, ...) starts against whatever schema is already there —
an empty database on first deploy — and every request will fail once it reaches EF Core,
with nothing in the error naming a missing migration as the cause. A deployed environment
either sets `Database__MigrateOnStartup=true` (the Docker and Azure setups in
[docs/INSTALLATION.md](docs/INSTALLATION.md) rely on it) or runs an explicit migration step
(for example `dotnet ef database update`) before the API is started.

## Test users

The imported realm defines five users: one per realm role in the default tenant, plus an
admin of a second tenant (Mandant) for trying out tenant isolation:

| Username      | Password | Realm role  | Tenant         |
|---------------|----------|-------------|----------------|
| `admin.user`  | `admin`  | `admin`     | default tenant |
| `dispo.user`  | `dispo`  | `disponent` | default tenant |
| `fahrer.user` | `fahrer` | `fahrer`    | default tenant |
| `viewer.user` | `viewer` | `viewer`    | default tenant |
| `admin.zweite`| `zweite` | `admin`     | second tenant  |

Every user carries a `tenant_id` attribute that Keycloak maps into the access token; the
API isolates all data per tenant based on that claim (see
[docs/KEYCLOAK.md](docs/KEYCLOAK.md)). `admin.zweite` starts with an empty database view —
including an empty vehicle catalog — because all seed and test data belongs to the default
tenant.

**These credentials are for local development only. They must never be used in, or
reach, a deployed environment.**

To sign in to the Keycloak admin console itself (a separate `master`-realm account whose
password Aspire generates per machine), and for more on how the realm is wired, see
[docs/KEYCLOAK.md](docs/KEYCLOAK.md).

## Ports

| Resource         | Port                              |
|------------------|------------------------------------|
| Angular (`Web`)  | 4200                                |
| Vue (`VueWeb`)   | 4300                                |
| Keycloak         | 8080 (HTTPS)                       |
| API              | dynamic — see the Aspire dashboard |

## API endpoints

The two master-data aggregates expose the same shape of CRUD endpoint group:

| Method | Route                | Policy            | Notes                                              |
|--------|----------------------|--------------------|-----------------------------------------------------|
| POST   | `/api/vehicles`      | `MasterDataWrite` | Per-field validation errors (nested `equipment`/`commercial` fields keyed e.g. `Equipment.TankVolumeLiters`, `Commercial.PurchasePrice`); `409` on a duplicate plate |
| GET    | `/api/vehicles`      | `Read`             | Paged; filters: `page`, `pageSize`, `status`, `type` |
| GET    | `/api/vehicles/{id}` | `Read`             | `404` if not found                                   |
| PUT    | `/api/vehicles/{id}` | `MasterDataWrite` | `404` if not found, `409` on a duplicate plate       |
| DELETE | `/api/vehicles/{id}` | `MasterDataWrite` | `404` if not found; also removes the vehicle's special documents |
| GET    | `/api/vehicles/{id}/documents` | `Read` | Special documents, soonest expiry first; `404` for an unknown vehicle |
| POST   | `/api/vehicles/{id}/documents` | `MasterDataWrite` | `{documentType, lastCheckedOn, validUntil, remark}`; `400` keyed by field, `409` if the vehicle already has that type |
| PUT    | `/api/vehicles/{id}/documents/{documentId}` | `MasterDataWrite` | Same body; `404` if not found, `409` on a duplicate type |
| DELETE | `/api/vehicles/{id}/documents/{documentId}` | `MasterDataWrite` | `404` if not found |
| GET    | `/api/vehicle-catalog/{kind}` | `Read`    | `kind` = `VehicleClass`, `CarrierGroup`, `InsuranceCompany` or `DocumentType`; all entries incl. inactive, with usage count |
| POST   | `/api/vehicle-catalog/{kind}` | `MasterDataWrite` | `{code, name, isActive}`; `409` on a duplicate code |
| PUT    | `/api/vehicle-catalog/{kind}/{id}` | `MasterDataWrite` | `{name, isActive}` (code is fixed); `404` if not found |
| DELETE | `/api/vehicle-catalog/{kind}/{id}` | `MasterDataWrite` | `404` if not found, `409` while vehicles (or their documents) use the entry |
| POST   | `/api/drivers`       | `MasterDataWrite` | Per-field validation errors                          |
| GET    | `/api/drivers`       | `Read`             | Paged; filters: `page`, `pageSize`, `status`         |
| GET    | `/api/drivers/{id}`  | `Read`             | `404` if not found                                   |
| PUT    | `/api/drivers/{id}`  | `MasterDataWrite` | `404` if not found                                   |
| DELETE | `/api/drivers/{id}`  | `MasterDataWrite` | `404` if not found                                   |

`Read` is satisfied by any of the four realm roles (`admin`, `disponent`, `fahrer`,
`viewer`). `MasterDataWrite` is satisfied only by `admin` — a signed-in `disponent`,
`fahrer` or `viewer` gets a `403` from these write endpoints. Both frontends currently
show the Add/Edit/Delete controls to every signed-in user regardless of role (there is no
role-decoding in either SPA yet) and rely on that `403` to refuse a non-admin's attempt,
surfaced as an error message rather than a hidden button — see the operator guides for
what that looks like.

Transport orders are dispatch data rather than master data, so their writes sit behind a
different policy:

| Method | Route                       | Policy          | Notes                                                        |
|--------|-----------------------------|-----------------|--------------------------------------------------------------|
| POST   | `/api/orders`               | `DispatchWrite` | Per-field validation errors; the order number is server-assigned |
| GET    | `/api/orders`               | `Read`          | Paged; filters: `page`, `pageSize`, `status`, `pickupFrom`, `pickupTo` |
| GET    | `/api/orders/{id}`          | `Read`          | `404` if not found                                            |
| PUT    | `/api/orders/{id}`          | `DispatchWrite` | `404` if not found, `409` if the order is no longer a `Draft` |
| POST   | `/api/orders/{id}/cancel`   | `DispatchWrite` | `404` if not found, `409` from `InTransit`, `Delivered` or an already-cancelled order |

`DispatchWrite` is satisfied by `admin` and `disponent` — a dispatcher who could not
create an order could not do their job, which is exactly why these endpoints do **not**
use `MasterDataWrite`.

### There is no DELETE for an order

Cancelling is `POST /api/orders/{id}/cancel`, not `DELETE /api/orders/{id}`, and that is
deliberate: an order is never removed. A haulier keeps the record of an order that was
placed and then withdrawn — for billing questions, for disputes, and because the order
number was already quoted to a customer. Cancelling is therefore a state transition, and
the endpoint returns the order's new state in the response body so a caller can refresh a
list straight from it instead of re-fetching.

The transition is also refused rather than forced. An order that is already `InTransit`
describes goods that are physically moving, and a `Delivered` order is final, so both
answer `409` with a message naming the current status. The same applies to editing: `PUT`
is accepted only while the order is still a `Draft`.

### Tours

Tours are dispatch data as well, but starting and completing one is also open to the driver who
is actually on the road:

| Method | Route                                  | Policy            | Notes                                                            |
|--------|----------------------------------------|-------------------|------------------------------------------------------------------|
| POST   | `/api/tours`                           | `DispatchWrite`   | `409` if the vehicle or driver is unavailable, the licence has expired, or either is already booked that day |
| GET    | `/api/tours`                           | `Read`            | Paged; filters: `page`, `pageSize`, `tourDate`, `vehicleId`, `driverId` |
| GET    | `/api/tours/{id}`                      | `Read`            | `404` if not found, `403` for a driver asking about someone else's tour |
| POST   | `/api/tours/{id}/orders`               | `DispatchWrite`   | Assigns an order; `409` on exceeded capacity or a tour already under way |
| DELETE | `/api/tours/{id}/orders/{orderId}`     | `DispatchWrite`   | Removes it and returns the order to `Draft`                       |
| POST   | `/api/tours/{id}/start`                | `TourStatusWrite` | Moves every assigned order to `InTransit`                         |
| POST   | `/api/tours/{id}/complete`             | `TourStatusWrite` | Moves every assigned order to `Delivered`                         |

`TourStatusWrite` is satisfied by `admin`, `disponent` and `fahrer` — but a `fahrer` may only
start and complete **their own** tours, and sees only their own in the list. That half of the
rule is enforced in the handlers rather than in the policy, because a policy sees the request
and not which tour it addresses. The link is the driver's `externalUserId`, which stores the
Keycloak `sub` claim; a driver record with no `externalUserId` belongs to nobody who can sign
in, and no driver can start its tours.

Removing an order from a tour is a `DELETE`, unlike cancelling an order. The two differ because
the outcomes differ: a cancelled order is a fact the haulier keeps, whereas a stop that was
planned and then unplanned leaves nothing worth recording — the order simply returns to `Draft`
and can be planned onto another tour.

### Double booking is refused by the database, not by a check

A vehicle and a driver may each have at most one tour per day. That is enforced by unique
indexes on `(tour_date, vehicle_id)` and `(tour_date, driver_id)`, not by a "is this vehicle
free?" query before the insert. The query would be a read-then-write race: two dispatchers
assigning the same lorry at the same moment would both read "free" and both insert. Only the
index actually serialises them. The repository translates PostgreSQL's unique violation into a
`409` naming which of the two is already booked.

### Tours are deliberately not cached

Like orders, and for the same reason: they change on every status transition, so the
invalidation traffic would cost more than the cached reads save, and a stale dispatch board is
worse than a slightly slower one. No tour handler injects `ICacheService`.

### Authorization defaults to fail closed

Every endpoint requires an authenticated user unless it explicitly opts out
(`/health`, `/alive`, the OpenAPI document and the Scalar UI, which run without a token
so Aspire's health probes and local API exploration keep working). This is enforced by an
ASP.NET fallback policy, which is applied to *any* request that carries no endpoint
metadata — including a request that matches no route at all. **A consequence worth
knowing as an API consumer: an unmatched route answers `401`, not `404`, for an
unauthenticated caller.** This is deliberate (see the design spec, §9) — an
unauthenticated `404` would let a caller probe which routes exist; a uniform `401` does
not. The price is that a typo'd URL also comes back as `401` rather than the usually more
informative `404`.

### Route planning and driving times

`GET /api/tours/{id}/route` plans the truck route through the tour's stops in sequence and lays the
driver's day on a timeline. `GET /api/orders/{id}/locations` places the consignor and consignee on a
map. By default neither calls an external service: the AppHost starts a self-hosted OpenRouteService
container (profile `driving-hgv`, so trucks avoid roads closed to them) and a self-hosted Nominatim
container for geocoding. Both read one OpenStreetMap extract that you download once before the first start:

```bash
curl -L -o data/osm/region.osm.pbf https://download.geofabrik.de/europe/germany/bayern-latest.osm.pbf
```

The first start builds the routing graph and imports the geocoding database, which takes from minutes
(routing) to hours (geocoding); see the developer guide, section 4.1, for sizing and changing the region.
Until the containers are ready, and for addresses outside the extract, the endpoints answer `503` or
`404`. A route is never estimated from straight-line distances, because that would ignore truck
restrictions.

Both services can instead use their public counterparts — routing `api.openrouteservice.org`
(needs a free API key, rate-limited quota), geocoding `nominatim.openstreetmap.org` (no key,
strict fair-use policy) — with worldwide coverage and no local import: set
`OpenRouteService:Mode` to `Online` in the AppHost user secrets together with an API key in
`Parameters:ors-api-key`. Geocoding follows the routing choice unless `Nominatim:Mode` pins it
separately, and the AppHost does not start the container of an online service. Details in the
developer guide, section 4.1; step-by-step setup including the registration (German) in
[docs/ONLINE_KARTENDIENSTE.md](docs/ONLINE_KARTENDIENSTE.md).

Addresses the geocoder does not know can be given coordinates by hand on the geodata maintenance
page (`/geodata` in both frontends). `GET /api/geodata` (`Read`) lists every address used by orders
and customer locations with its resolution status; `PUT /api/geodata/overrides` and
`DELETE /api/geodata/overrides/{id}` (`MasterDataWrite`) maintain manual overrides, and
`POST /api/geodata/resolve` (`Read`) probes a single address. A manual override takes precedence
over the geocoder everywhere a position is resolved. The same page shows the state of the map
material: `GET /api/geodata/map-status` (`Read`) probes both map services and reports their
availability and OSM data date, plus the installed extract (region, file date, size — parsed from
the PBF header); replacing the extract itself is a server-side task
(`scripts/update-osm-extract.py`, see the developer guide).

The timeline follows Regulation (EC) No 561/2006: a 45-minute break after 4 h 30 min of continuous
driving, 9 h daily driving (10 h is flagged as an extension, allowed twice a week), and the 11 h
daily rest that follows. Countries outside the EU, EEA, Switzerland and Great Britain are reported
as not modelled, not planned as if EU rules applied. Two national rules are modelled: the Sunday
truck driving ban in Germany and Austria, which applies to every tour because a tour is always driven by a tractor unit. Holidays and weekly
driving and rest limits are not modelled because a single tour cannot see them. The values in
`DrivingRules` need checking against current law before anyone relies on them for compliance.

## Caching

List and get-by-id reads on both aggregates are cached in Redis; every write invalidates
its aggregate's cache entries by key prefix (so a single write drops every cached page and
filter combination for that aggregate, not just the one row it touched). **Caching is
disabled entirely when no Redis connection string is configured** (this is the case for
the API integration tests, and would be the case for any environment Aspire didn't wire a
Redis resource into) — cache writes are skipped outright rather than silently degrading to
an unbounded, unindexed, never-invalidated cache. Caching without the ability to
invalidate is a correctness hazard, not a performance win worth keeping.

**Transport orders and tours are deliberately not cached.** They change far more often than a
vehicle or a driver does — every status transition rewrites one — so the invalidation
traffic would cost more than the cached reads save, and a stale dispatch list is worse
than a slightly slower one. No order handler injects `ICacheService`, and no order write
invalidates anything.

## API response language

FluentValidation's built-in validation messages follow the ambient culture of the machine
running the API, which made responses non-deterministic: the same request answered in
German on a German-locale development machine and in English in CI. `Program.cs` now pins
this explicitly to English (invariant culture) at startup, so behaviour is identical
everywhere:

```csharp
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
```

**This resolves a non-determinism, it is not a decision that English is the final
product-facing language.** TransBrain is a German haulier, and validation messages in
German may well be the eventual product decision — that question has been raised
separately and is not settled by this change. To switch the API's validation messages to
German, change both lines above (in `src/TransBrain.Api/Program.cs`) to
`CultureInfo.GetCultureInfo("de-DE")`.

## Running the tests

Domain, Application and the Testcontainers-backed API integration tests (732 tests at
time of writing: 275 Domain, 324 Application, 133 API integration) all run with:

```bash
dotnet test TransBrain.slnx
```

This requires Docker to be running (for the integration tests).

Each frontend additionally has Playwright end-to-end tests (12 spec files per frontend, 46
tests, covering the role-based home page for all four realm roles, sign-in and sign-out, the
route guards, and the vehicle, driver, customer, order, tour and geodata screens):

```bash
npm run e2e
```

run from `src/TransBrain.Web` or `src/TransBrain.VueWeb`. These need the full stack
running (`dotnet run --project src/TransBrain.AppHost`, including a trusted dev
certificate — see above) since they exercise the real OIDC login flow against Keycloak.
They are not run in CI yet — see `.github/workflows/ci.yml` for why.

Both frontends' `playwright.config.ts` also register a `globalSetup` (`e2e/warm-up.ts`) that
signs the dev server's and Keycloak's cold-start cost off once before the first test. Without
it that cost - over 30s right after a source change, against under 2s for every later sign-in -
lands entirely on whichever test authenticates first and fails it. It is best-effort: if the
warm-up itself fails it logs and steps aside rather than aborting the run.

Both frontends' `playwright.config.ts` pin `workers: 1` deliberately, not as an
unoptimised default: every spec authenticates through the same Keycloak realm/container,
and concurrent logins against that one container time out intermittently under the
default parallel workers — observed directly during development, and the failure looks
like a broken test rather than contention, which is worse than the slower serialised run.

## Application-layer coverage gate

Spec §11 sets an 80% line-coverage floor for the Application layer. The backend CI job
enforces it: it runs `TransBrain.Application.Tests` with `--collect:"XPlat Code Coverage"`
and fails if the resulting line coverage drops below 80%. Measured at the time this gate
was added, the suite sits at 86.9% line coverage (84.7% branch) — comfortably above the
floor. The threshold is intentionally left at the spec's 80%, not raised to the current
number: a gate that fails the moment someone adds a single untested private helper trains
people to disable gates rather than write tests.
