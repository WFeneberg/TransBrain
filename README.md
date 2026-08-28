# TransBrain

Dispatch software for a trucking company: vehicles, drivers, transport orders and tours,
built with .NET 10 (Clean Architecture, a hand-rolled CQRS mediator, EF Core 10 /
PostgreSQL, Redis) behind Keycloak-issued OIDC tokens, with two frontends — Angular and
Vue — talking to the same API.

This is Phase 1 (foundation and walking skeleton): a `Vehicle` aggregate with `Create`
and `List`, wired end to end through both frontends. See [CHANGELOG.md](CHANGELOG.md)
for what exists today, and `.superpowers/sdd/` for the phase plans.

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

## Running the tests

Domain, Application and the Testcontainers-backed API integration tests (47 tests at
time of writing) all run with:

```bash
dotnet test TransBrain.slnx
```

This requires Docker to be running (for the integration tests).

Each frontend additionally has Playwright end-to-end tests:

```bash
npm run e2e
```

run from `src/TransBrain.Web` or `src/TransBrain.VueWeb`. These need the full stack
running (`dotnet run --project src/TransBrain.AppHost`, including a trusted dev
certificate — see above) since they exercise the real OIDC login flow against Keycloak.
They are not run in CI yet — see `.github/workflows/ci.yml` for why.
