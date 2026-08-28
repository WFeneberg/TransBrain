using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TransBrain.Api.Endpoints;
using TransBrain.Application;
using TransBrain.Infrastructure;
using TransBrain.Infrastructure.Persistence;

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

WebApplication app = builder.Build();

app.MapDefaultEndpoints();
app.UseCors();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

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
