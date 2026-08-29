using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TransBrain.Api.Authorization;
using TransBrain.Api.Endpoints;
using TransBrain.Application;
using TransBrain.Infrastructure;
using TransBrain.Infrastructure.Persistence;
using TransBrain.ServiceDefaults;

// FluentValidation resolves its built-in error messages from the ambient thread culture, so
// without these two lines the same request answers in German on a German-locale developer
// machine and in English in CI, which is non-determinism, not a language choice. Pinned to
// English (invariant culture) here so behaviour is identical everywhere and matches this
// codebase's English-for-code convention. This is NOT a final decision that the API's
// product-facing language is English — TransBrain is a German haulier and that may change.
// See README.md, section "API response language", for the two lines below to flip together
// to switch validation messages to German instead.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<TransBrainDbContext>("transbraindb");

// Redis is registered only when Aspire supplied a connection string. The integration
// tests run without a Redis container and fall through to the in-memory cache.
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("cache")))
{
    builder.AddRedisDistributedCache("cache");
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddKeycloakJwtBearer("keycloak", realm: "transbrain", options =>
    {
        options.Audience = "transbrain-api";
        // Written against Development, not Production: any other environment name (Staging, QA,
        // a customer pilot, ...) must still require HTTPS for the OIDC discovery document and
        // signing keys. Gating on IsProduction() instead would relax every non-Production
        // environment, leaving realistic pre-production environments open to a signing-key
        // substitution over plain HTTP.
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.Events = new JwtBearerEvents
        {
            // Keycloak nests realm roles under a "realm_access.roles" claim, which ASP.NET
            // Core does not map to ClaimTypes.Role on its own. Without this mapping, every
            // RequireRole policy check silently fails, which looks like a permissions bug
            // rather than a missing claims-mapping bug.
            OnTokenValidated = context =>
            {
                if (context.Principal?.Identity is not ClaimsIdentity identity)
                {
                    return Task.CompletedTask;
                }

                ILogger logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("TransBrain.Api.Authentication");

                string? realmAccess = context.Principal.FindFirst("realm_access")?.Value;
                if (string.IsNullOrWhiteSpace(realmAccess))
                {
                    logger.LogWarning("Token validated with no 'realm_access' claim present; no realm roles were mapped.");
                    return Task.CompletedTask;
                }

                // A malformed claim (bad JSON, or "roles" not shaped as an array) must not throw here:
                // OnAuthenticationFailed is not overridden, so an exception would escape token validation
                // and surface as a raw 500. Falling back to "no roles mapped" instead lets the request
                // continue to authorization, which then fails cleanly with 403.
                List<string> mappedRoles = [];
                try
                {
                    using JsonDocument document = JsonDocument.Parse(realmAccess);
                    if (document.RootElement.TryGetProperty("roles", out JsonElement roles) &&
                        roles.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement role in roles.EnumerateArray())
                        {
                            string? value = role.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                identity.AddClaim(new Claim(ClaimTypes.Role, value));
                                mappedRoles.Add(value);
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    logger.LogWarning("Token validated with an unreadable 'realm_access' claim; no realm roles were mapped.");
                    return Task.CompletedTask;
                }

                logger.LogDebug("Mapped realm roles from token: {Roles}", string.Join(", ", mappedRoles));

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    // Fail closed: an endpoint that forgets RequireAuthorization is refused rather than
    // silently public. The infrastructure endpoints below opt out explicitly.
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(Policies.MasterDataWrite, policy => policy.RequireRole("admin"))
    .AddPolicy(Policies.DispatchWrite, policy => policy.RequireRole("admin", "disponent"))
    .AddPolicy(Policies.TourStatusWrite, policy => policy.RequireRole("admin", "disponent", "fahrer"))
    .AddPolicy(Policies.Read, policy => policy.RequireRole("admin", "disponent", "fahrer", "viewer"));

WebApplication app = builder.Build();

app.MapDefaultEndpoints();
app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();

    // No retry here: this assumes PostgreSQL is already reachable. Aspire's WaitFor in the
    // AppHost (Task 11) is what guarantees the database is up before the Api starts, so a
    // cold start where PostgreSQL isn't ready yet will throw here rather than retry.
    using IServiceScope scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<TransBrainDbContext>().Database.MigrateAsync();
}

foreach (IEndpointGroup group in Assembly.GetExecutingAssembly().GetTypes()
             .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IEndpointGroup).IsAssignableFrom(t))
             .Select(Activator.CreateInstance)
             .Cast<IEndpointGroup>())
{
    group.Map(app);
}

await app.RunAsync();

public partial class Program;
