var builder = DistributedApplication.CreateBuilder(args);

// Persistent data volume. The volume keeps the password from the run that created it, so the
// generated postgres password must stay stable: it lives in the AppHost's user secrets
// (Parameters:postgres-password, see UserSecretsId in the csproj). If those secrets are ever
// cleared, Postgres rejects every connection with "password authentication failed" and the
// API hangs in `Waiting` — fix by restoring the old password or deleting the volume
// (`docker volume ls | grep postgres`, then `docker volume rm <name>`).
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var database = postgres.AddDatabase("transbraindb");

var cache = builder.AddRedis("cache");

// Deliberately no .WithDataVolume() for Keycloak (unlike postgres above). Keycloak's directory
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

// Self-hosted routing and geocoding, so no external service is called at runtime. Both read the
// same OpenStreetMap extract, downloaded once by hand to data/osm/region.osm.pbf (see README).
// The first start builds the routing graph (minutes) and imports the geocoding database (hours
// for a German state); the results live in the named volumes. To switch the region, replace the
// file and delete both volumes. The api deliberately does not WaitFor these: until they are
// ready its map endpoints answer 503.
const string osmDirectory = "../../data/osm";

// Elevation is off because building it downloads height data from external servers.
// XMX is the knob for larger extracts: roughly <pbf size> * 2 for the one enabled profile.
var ors = builder.AddContainer("ors", "openrouteservice/openrouteservice", "v10.1.0")
    .WithHttpEndpoint(targetPort: 8082)
    .WithVolume("transbrain-ors", "/home/ors")
    .WithBindMount(osmDirectory, "/home/ors/files", isReadOnly: true)
    .WithEnvironment("XMX", "4g")
    .WithEnvironment("ors.engine.profile_default.build.source_file", "/home/ors/files/region.osm.pbf")
    .WithEnvironment("ors.engine.profile_default.build.elevation", "false")
    .WithEnvironment("ors.engine.profiles.driving-car.enabled", "false")
    .WithEnvironment("ors.engine.profiles.driving-hgv.enabled", "true")
    .WithHttpHealthCheck("/ors/v2/health");

// The extract is mounted outside /nominatim: the image chowns that folder recursively on start,
// which fails on a read-only mount.
var nominatim = builder.AddContainer("nominatim", "mediagis/nominatim", "5.3")
    .WithHttpEndpoint(targetPort: 8080)
    .WithVolume("transbrain-nominatim", "/var/lib/postgresql/16/main")
    .WithBindMount(osmDirectory, "/osm", isReadOnly: true)
    .WithContainerRuntimeArgs("--shm-size=1g")
    .WithEnvironment("PBF_PATH", "/osm/region.osm.pbf")
    .WithEnvironment("FREEZE", "true")
    .WithHttpHealthCheck("/status");

var api = builder.AddProject<Projects.TransBrain_Api>("api")
    .WithEnvironment("OpenRouteService__BaseUrl", ReferenceExpression.Create($"{ors.GetEndpoint("http")}/ors/"))
    .WithEnvironment("Nominatim__BaseUrl", ReferenceExpression.Create($"{nominatim.GetEndpoint("http")}/"))
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
