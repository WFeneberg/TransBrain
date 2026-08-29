# Keycloak — developer reference

How authentication is wired for local development: the realm, the admin console, and the
accounts you can sign in with. Everything here is **local development only** — see the
warnings at the end.

## The realm is imported from a file on every start

Keycloak runs as a container started by the AppHost (`src/TransBrain.AppHost/AppHost.cs`,
`builder.AddKeycloak("keycloak", 8080).WithRealmImport("./realms")`). On every start it
imports `src/TransBrain.AppHost/realms/transbrain-realm.json`, which is the authoritative
definition of the `transbrain` realm — its roles, its two clients (`transbrain-api`,
`transbrain-spa`) and its users.

There is **no data volume** attached to the container (removed deliberately, see the
comment in `AppHost.cs`). Consequences:

- Every run re-imports the realm fresh from `transbrain-realm.json`, so an edit to that
  file always takes effect on the next start.
- Anything you change through the admin console — a new user, a client tweak, a password
  reset — is gone after a restart. Change `transbrain-realm.json` instead if you want it
  to stick.

The realm's authority is `https://localhost:8080/realms/transbrain` over HTTPS, using the
ASP.NET Core / Aspire self-signed development certificate. Run `dotnet dev-certs https
--trust` once per machine or OIDC discovery fails before you ever see a login screen (see
the README).

## Keycloak admin console

Use this to inspect users, roles, client config, sessions and tokens while debugging
auth.

| | |
|---|---|
| URL | `https://localhost:8080/admin/` |
| Username | `admin` |
| Password | generated per machine by Aspire — see below |

The admin account is the Keycloak bootstrap admin in the **`master`** realm, not a user
of the `transbrain` realm. Aspire generates its password on first run and stores it as a
user-secret of the AppHost project. It stays the same across restarts on the same
machine, and differs from machine to machine.

Retrieve the current value:

```bash
dotnet user-secrets list --project src/TransBrain.AppHost | grep keycloak-password
# -> Parameters:keycloak-password = <the password>
```

Or read it from the running stack: Aspire dashboard → **keycloak** resource → **Details**
→ environment variable `KC_BOOTSTRAP_ADMIN_PASSWORD`.

To reset it, delete the `Parameters:keycloak-password` secret (`dotnet user-secrets
remove "Parameters:keycloak-password" --project src/TransBrain.AppHost`); Aspire
generates a new one on the next start.

After signing in, switch from the `master` realm to `transbrain` using the realm picker
at the top of the left-hand nav to see this project's users and clients.

## Test users (the `transbrain` realm)

Defined in `transbrain-realm.json`, one per realm role. These are what you log into the
Angular (`http://localhost:4200`) and Vue (`http://localhost:4300`) frontends with.

| Username      | Password | Realm role  | Can do                                                    |
|---------------|----------|-------------|----------------------------------------------------------|
| `admin.user`  | `admin`  | `admin`     | Everything, including master-data writes (`MasterDataWrite`) |
| `dispo.user`  | `dispo`  | `disponent` | Read everything; create/edit orders and tours (`DispatchWrite`) |
| `fahrer.user` | `fahrer` | `fahrer`    | Read; start/complete **their own** tours (`TourStatusWrite`) |
| `viewer.user` | `viewer` | `viewer`    | Read only                                                  |

A `fahrer` is tied to a `Driver` record through `externalUserId`, which stores the
Keycloak `sub` claim. A driver row with no `externalUserId` belongs to nobody who can
sign in, and cannot start its tours. See the README's "API endpoints" section for the
full policy-to-role mapping.

## Clients

| Client ID        | Type       | Notes                                                              |
|------------------|------------|-------------------------------------------------------------------|
| `transbrain-spa` | public     | Authorization code + PKCE (S256); redirect/web-origin URIs for ports 4200 and 4300; adds a `transbrain-api` audience mapper to the access token |
| `transbrain-api` | bearer-only | The API validates tokens; it never initiates a login             |

## This is development-only configuration

`transbrain-realm.json` sets `sslRequired: "none"` (tokens can be minted and accepted
over plain HTTP) and ships trivial, well-known passwords. Both are called out in the
realm file's `_comment`. This realm, these users, and the bootstrap admin credentials
**must never be used in, or reach, a deployed environment.**
