using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using TransBrain.Infrastructure.Persistence;

namespace TransBrain.Api.IntegrationTests;

public sealed class TransBrainApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync() => await _postgres.DisposeAsync();

    public HttpClient CreateClientAs(params string[] roles)
    {
        HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return client;
    }

    /// <summary>
    /// A client that is a SPECIFIC user, not just some holder of a role — needed by the tour
    /// tests, where a driver may only touch their own tours.
    /// </summary>
    /// <remarks>
    /// Deliberately not an overload of <see cref="CreateClientAs"/>: a
    /// <c>CreateClientAs(string, params string[])</c> would be ambiguous with the params-only
    /// version at every single-string call site.
    /// </remarks>
    public HttpClient CreateClientAsSubject(string subject, params string[] roles)
    {
        HttpClient client = CreateClientAs(roles);
        client.DefaultRequestHeaders.Add(TestAuthHandler.SubjectHeader, subject);
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("ConnectionStrings:transbraindb", _postgres.GetConnectionString());

        // No `cache` connection string on purpose: Program.cs falls back to the
        // in-memory distributed cache when Aspire has not supplied one.

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }
}
