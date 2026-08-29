# TransBrain

Dispatch software for a trucking company: vehicles, drivers, transport orders and tours,
built with .NET 10 (Clean Architecture, a hand-rolled CQRS mediator, EF Core 10 /
PostgreSQL, Redis) behind Keycloak-issued OIDC tokens, with two frontends — Angular and
Vue — talking to the same API.

Phase 1 (foundation and walking skeleton) delivered a `Vehicle` aggregate with `Create`
and `List`, wired end to end through both frontends. Phase 2 (master data completion)
added a full `Driver` aggregate, completed vehicle CRUD (`Update`, `Delete`, `GetById`),
added list filters, Redis caching, per-field validation errors and a fallback
authorization policy. See [CHANGELOG.md](CHANGELOG.md) for the detailed list of what
exists today, and `.superpowers/sdd/` for the phase plans.

## Prerequisites

- .NET 10 SDK
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

### Data does not survive a restart

Neither Postgres nor Keycloak has a data volume attached — both were removed
deliberately. Postgres re-runs EF Core migrations from scratch on every start, and
Keycloak re-imports `src/TransBrain.AppHost/realms/transbrain-realm.json` on every start.
This is intentional: it is what makes a run reproducible, and it is what keeps the realm
file (rather than whatever accumulated in a volume) the authoritative source of realm
configuration. Do not expect vehicles you created, or realm edits you made through the
Keycloak admin console, to still be there after a restart.

### Database migrations are only applied automatically in Development

`Program.cs` calls `Database.MigrateAsync()` only when `IsDevelopment()` is true. Any other
environment (Staging, QA, production, ...) starts against whatever schema is already there —
an empty database on first deploy — and every request will fail once it reaches EF Core,
with nothing in the error naming a missing migration as the cause. A deployed environment
needs an explicit migration step (for example `dotnet ef database update`, or running
migrations as part of the deployment pipeline) before the API is started. This is not wired
up yet; it is a known gap for the next phase, not an oversight.

## Test users

The imported realm defines four users, one per realm role:

| Username      | Password | Realm role  |
|---------------|----------|-------------|
| `admin.user`  | `admin`  | `admin`     |
| `dispo.user`  | `dispo`  | `disponent` |
| `fahrer.user` | `fahrer` | `fahrer`    |
| `viewer.user` | `viewer` | `viewer`    |

**These credentials are for local development only. They must never be used in, or
reach, a deployed environment.**

## Ports

| Resource         | Port                              |
|------------------|------------------------------------|
| Angular (`Web`)  | 4200                                |
| Vue (`VueWeb`)   | 4300                                |
| Keycloak         | 8080 (HTTPS)                       |
| API              | dynamic — see the Aspire dashboard |

## API endpoints

Both aggregates expose the same shape of CRUD endpoint group:

| Method | Route                | Policy            | Notes                                              |
|--------|----------------------|--------------------|-----------------------------------------------------|
| POST   | `/api/vehicles`      | `MasterDataWrite` | Per-field validation errors; `409` on a duplicate plate |
| GET    | `/api/vehicles`      | `Read`             | Paged; filters: `page`, `pageSize`, `status`, `type` |
| GET    | `/api/vehicles/{id}` | `Read`             | `404` if not found                                   |
| PUT    | `/api/vehicles/{id}` | `MasterDataWrite` | `404` if not found, `409` on a duplicate plate       |
| DELETE | `/api/vehicles/{id}` | `MasterDataWrite` | `404` if not found                                   |
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

## Caching

List and get-by-id reads on both aggregates are cached in Redis; every write invalidates
its aggregate's cache entries by key prefix (so a single write drops every cached page and
filter combination for that aggregate, not just the one row it touched). **Caching is
disabled entirely when no Redis connection string is configured** (this is the case for
the API integration tests, and would be the case for any environment Aspire didn't wire a
Redis resource into) — cache writes are skipped outright rather than silently degrading to
an unbounded, unindexed, never-invalidated cache. Caching without the ability to
invalidate is a correctness hazard, not a performance win worth keeping.

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

Domain, Application and the Testcontainers-backed API integration tests (138 tests at
time of writing: 42 Domain, 67 Application, 29 API integration) all run with:

```bash
dotnet test TransBrain.slnx
```

This requires Docker to be running (for the integration tests).

Each frontend additionally has Playwright end-to-end tests (5 specs per frontend, covering
login, the vehicle list/form and the driver list/form):

```bash
npm run e2e
```

run from `src/TransBrain.Web` or `src/TransBrain.VueWeb`. These need the full stack
running (`dotnet run --project src/TransBrain.AppHost`, including a trusted dev
certificate — see above) since they exercise the real OIDC login flow against Keycloak.
They are not run in CI yet — see `.github/workflows/ci.yml` for why.

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
