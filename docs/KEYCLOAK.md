# Keycloak — developer reference

How authentication is wired for local development: the realm, the admin console, and the
accounts you can sign in with. Everything here is **local development only** — see the
warnings at the end.

## The realm is imported from a file on every start

Keycloak runs as a container started by the AppHost (`src/TransBrain.AppHost/AppHost.cs`,
`builder.AddKeycloak("keycloak", 8080).WithRealmImport("./realms")`). On every start it
imports `src/TransBrain.AppHost/realms/transbrain-realm.json`, which is the authoritative
definition of the `transbrain` realm — its roles, its three clients (`transbrain-api`,
`transbrain-spa`, `transbrain-mobile`) and its users.

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

Defined in `transbrain-realm.json`, one per realm role plus a second-tenant admin. These
are what you log into the Angular (`http://localhost:4200`) and Vue
(`http://localhost:4300`) frontends with.

| Username      | Password | Realm role  | Can do                                                    |
|---------------|----------|-------------|----------------------------------------------------------|
| `admin.user`  | `admin`  | `admin`     | Everything, including master-data writes (`MasterDataWrite`) |
| `dispo.user`  | `dispo`  | `disponent` | Read everything; create/edit orders and tours (`DispatchWrite`) |
| `fahrer.user` | `fahrer` | `fahrer`    | Read; start/complete **their own** tours (`TourStatusWrite`) |
| `viewer.user` | `viewer` | `viewer`    | Read only                                                  |
| `admin.zweite`| `zweite` | `admin`     | Everything — but only inside the **second tenant** (see below) |

A `fahrer` is tied to a `Driver` record through `externalUserId`, which stores the
Keycloak `sub` claim. A driver row with no `externalUserId` belongs to nobody who can
sign in, and cannot start its tours. See the README's "API endpoints" section for the
full policy-to-role mapping.

## Tenants (Mandanten): the `tenant_id` attribute and claim

TransBrain is multi-tenant: every business row belongs to exactly one tenant, and the API
only ever reads and writes the caller's tenant. The tenant comes out of the **signed
access token**, never out of anything a caller can set freely:

- Each realm user carries a `tenant_id` **user attribute** holding the tenant's GUID.
- A `tenant-id` **protocol mapper** (`oidc-usermodel-attribute-mapper`) on the
  `transbrain-spa` and `transbrain-mobile` clients copies that attribute into a
  `tenant_id` claim in the access, ID and userinfo tokens. The mapper exists in both
  realm files
  (`src/TransBrain.AppHost/realms/transbrain-realm.json` and
  `deploy/keycloak/transbrain-realm.json`).
- The API resolves the claim per request (`HttpContextTenantContext`) and fails closed:
  an authenticated request **without** a valid `tenant_id` claim is answered `403` before
  any handler runs (`TenantRequirementMiddleware`).

The dev realm ships two tenants:

| Tenant         | GUID                                   | Users                               |
|----------------|----------------------------------------|-------------------------------------|
| default tenant | `0199c0de-0000-7000-8000-000000000001` | `admin.user`, `dispo.user`, `fahrer.user`, `viewer.user` |
| second tenant  | `0199c0de-0000-7000-8000-000000000002` | `admin.zweite`                      |

All pre-tenancy data, every seeded catalog entry and the test users' data belong to the
default tenant; `admin.zweite` sees an empty system and builds its own master data.

To give a **new** user a tenant in the admin console: user → **Attributes** tab → add key
`tenant_id` with the tenant GUID as value. On Keycloak 24+ the attributes tab may hide
"unmanaged" attributes depending on the realm's user-profile settings; if `tenant_id` is
not offered, enable unmanaged attributes (Realm settings → General → Unmanaged
attributes → `Enabled`) or declare it in the user profile. Remember that console edits do
not survive a restart (no data volume) — put permanent users into
`transbrain-realm.json` instead.

Known limitation: the invoice seller identity (`InvoiceSeller` configuration section) is
global application configuration, not per-tenant — all tenants' invoices currently carry
the same seller letterhead.

## Clients

| Client ID        | Type       | Notes                                                              |
|------------------|------------|-------------------------------------------------------------------|
| `transbrain-spa` | public     | Authorization code + PKCE (S256); redirect/web-origin URIs for ports 4200 and 4300; adds a `transbrain-api` audience mapper to the access token |
| `transbrain-mobile` | public  | Same flow and mappers as `transbrain-spa`, for the Flutter driver app (`src/TransBrain.DriverApp`); redirect URIs `http://localhost:4400/*` (web demo) and `de.transbrain.driver://callback` (Android/iOS deep link) |
| `transbrain-api` | bearer-only | The API validates tokens; it never initiates a login             |

## This is development-only configuration

`transbrain-realm.json` sets `sslRequired: "none"` (tokens can be minted and accepted
over plain HTTP) and ships trivial, well-known passwords. Both are called out in the
realm file's `_comment`. This realm, these users, and the bootstrap admin credentials
**must never be used in, or reach, a deployed environment.**
