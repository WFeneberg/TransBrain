var builder = DistributedApplication.CreateBuilder(args);

// Deliberately NO data volume, for the same reason Keycloak has none: a persisted volume
// keeps the password from the run that created it, while Aspire supplies the current one.
// When the two drift apart Postgres rejects every connection with "password authentication
// failed", and everything with WaitFor(database) — including the API — hangs in `Waiting`
// with nothing naming the cause. This cost two separate debugging rounds during execution.
// Phase 1 has no seed data worth preserving; migrations rebuild the schema on every start,
// which is what makes a run reproducible.
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var database = postgres.AddDatabase("transbraindb");

var cache = builder.AddRedis("cache");

// Deliberately no .WithDataVolume() here (unlike postgres above). Keycloak's directory
// import only CREATES a realm that does not already exist; a persisted volume makes the
// checked-in realm file stop being the source of truth after the first run, and later
// edits to transbrain-realm.json are silently ignored ("Realm 'transbrain' already
// exists. Import skipped", logged as success). Without a volume, every run re-imports
// the realm fresh from disk, which is the whole point of committing it.
//
// Fixed host port 8080 above is honoured, but Aspire.Hosting.Keycloak unconditionally
// injects the trusted ASP.NET Core/Aspire dev HTTPS certificate into the container
// (KC_HTTPS_CERTIFICATE_FILE), so the endpoint that ends up reachable at that port is
// HTTPS, not HTTP. The stable authority for OIDC discovery/redirects is therefore
// https://localhost:8080/realms/transbrain (self-signed CN=localhost cert — trust it once
// via `dotnet dev-certs https --trust` on the dev machine), not the http:// URL the
// resource name might suggest.
// Pin the hostname explicitly: without it, Keycloak derives the issuer/frontend URL from
// whatever Host header a given caller happens to connect with, so a browser (arriving via
// the fixed proxied port above) and a caller on the internal container network (arriving as
// "keycloak:8080") get two DIFFERENT issuer strings for the same realm. Token validation
// compares the token's `iss` claim against the issuer the validating party's own discovery
// document reports, so both must agree on one fixed string. Since the browser-facing
// authority is https://localhost:8080 (see comment above), that is also what every other
// caller — including the Api, which runs as a plain host process and can reach this same
// proxied port directly — must see as the issuer.
var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithEnvironment("KC_HOSTNAME", "https://localhost:8080")
    .WithRealmImport("./realms");

var api = builder.AddProject<Projects.TransBrain_Api>("api")
    .WithReference(database).WaitFor(database)
    .WithReference(cache).WaitFor(cache)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

builder.AddViteApp("web", "../TransBrain.Web", "start")
    .WithNpm()
    .WithReference(api).WaitFor(api)
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
    .WithExternalHttpEndpoints();

builder.AddViteApp("vueweb", "../TransBrain.VueWeb")
    .WithNpm()
    .WithReference(api).WaitFor(api)
    .WithHttpEndpoint(port: 4300, targetPort: 4300, isProxied: false)
    .WithExternalHttpEndpoints();

builder.Build().Run();
