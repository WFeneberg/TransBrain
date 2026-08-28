var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var database = postgres.AddDatabase("transbraindb");

var cache = builder.AddRedis("cache");

var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume()
    .WithRealmImport("./realms");

var api = builder.AddProject<Projects.TransBrain_Api>("api")
    .WithReference(database).WaitFor(database)
    .WithReference(cache).WaitFor(cache)
    .WithReference(keycloak).WaitFor(keycloak)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

// TransBrain.Web does not exist yet: it is created by Task 14. Re-enable this block then.
// builder.AddViteApp("web", "../TransBrain.Web", "start")
//     .WithNpm()
//     .WithReference(api).WaitFor(api)
//     .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false)
//     .WithExternalHttpEndpoints();

// TransBrain.VueWeb does not exist yet: it is created by Task 15. Re-enable this block then.
// builder.AddViteApp("vueweb", "../TransBrain.VueWeb")
//     .WithNpm()
//     .WithReference(api).WaitFor(api)
//     .WithHttpEndpoint(port: 4300, targetPort: 4300, isProxied: false)
//     .WithExternalHttpEndpoints();

builder.Build().Run();
