using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TransBrain.Application.Features.Assistant;

namespace TransBrain.Api.IntegrationTests;

/// <summary>
/// The assistant feature end to end: chat and status are for every signed-in role, the settings
/// are system administration, and the settings endpoint really edits the agent's .env file in
/// its data volume — the single source of truth both containers share.
/// </summary>
public sealed class AssistantEndpointsTests(TransBrainApiFactory factory)
    : IClassFixture<TransBrainApiFactory>, IDisposable
{
    private readonly string _dataDirectory = Directory.CreateTempSubdirectory("hermes-test-").FullName;

    public void Dispose() => Directory.Delete(_dataDirectory, recursive: true);

    /// <summary>A host whose HermesAgent:DataDirectory points at this test's own temp volume.</summary>
    private WebApplicationFactory<Program> Provisioned() => factory.WithWebHostBuilder(builder =>
        builder.UseSetting("HermesAgent:DataDirectory", _dataDirectory));

    private static AssistantSettingsPayload APayload(
        bool enabled = true,
        string provider = "openrouter",
        string model = "anthropic/claude-sonnet-4.5",
        string? customBaseUrl = null,
        bool autonomy = false,
        string? apiKey = "sk-or-test") =>
        new(enabled, provider, model, customBaseUrl, autonomy, apiKey);

    private string EnvText() => File.ReadAllText(Path.Combine(_dataDirectory, ".env"));

    // ----- authorization ---------------------------------------------------------------------

    [Fact]
    public async Task GetStatus_Anonymous_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient().GetAsync("/api/assistant/status");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Chat_Anonymous_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await factory.CreateClient()
            .PostAsJsonAsync("/api/assistant/chat", new AssistantChatPayload(
                [new AssistantChatMessagePayload("user", "Hallo")], null));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("disponent")]
    [InlineData("fahrer")]
    [InlineData("viewer")]
    public async Task GetSettings_TenantRole_ReturnsForbidden(string role)
    {
        HttpResponseMessage response = await factory.CreateClientAs(role)
            .GetAsync("/api/system/assistant-settings");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateSettings_TenantAdmin_ReturnsForbidden()
    {
        HttpResponseMessage response = await factory.CreateClientAs("admin")
            .PutAsJsonAsync("/api/system/assistant-settings", APayload());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("disponent")]
    [InlineData("fahrer")]
    [InlineData("viewer")]
    public async Task GetStatus_AnyTenantRole_ReturnsOk(string role)
    {
        HttpResponseMessage response = await factory.CreateClientAs(role).GetAsync("/api/assistant/status");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ----- unprovisioned deployment (no data directory configured) ----------------------------

    [Fact]
    public async Task GetStatus_Unprovisioned_ReportsDisabled()
    {
        AssistantStatusResponse? status = await factory.CreateClientAs("viewer")
            .GetFromJsonAsync<AssistantStatusResponse>("/api/assistant/status");

        status!.Enabled.Should().BeFalse();
        status.Ready.Should().BeFalse();
    }

    [Fact]
    public async Task Chat_Unprovisioned_ReturnsServiceUnavailable()
    {
        HttpResponseMessage response = await factory.CreateClientAs("disponent")
            .PostAsJsonAsync("/api/assistant/chat", new AssistantChatPayload(
                [new AssistantChatMessagePayload("user", "Hallo")], null));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Chat_NoMessages_ReturnsBadRequest()
    {
        HttpResponseMessage response = await factory.CreateClientAs("viewer")
            .PostAsJsonAsync("/api/assistant/chat", new AssistantChatPayload([], null));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateSettings_Unprovisioned_ReturnsServiceUnavailable()
    {
        HttpResponseMessage response = await factory.CreateSystemAdminClient()
            .PutAsJsonAsync("/api/system/assistant-settings", APayload());

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    // ----- provisioned deployment: settings really edit the agent's .env ----------------------

    [Fact]
    public async Task UpdateSettings_SystemAdmin_RoundTripsAndWritesEnvFile()
    {
        using WebApplicationFactory<Program> host = Provisioned();
        HttpClient client = host.CreateSystemAdminClientFor();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/system/assistant-settings", APayload(autonomy: true));
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        AssistantSettingsResponse? saved = await response.Content.ReadFromJsonAsync<AssistantSettingsResponse>();
        saved!.Provisioned.Should().BeTrue();
        saved.Enabled.Should().BeTrue();
        saved.Provider.Should().Be("openrouter");
        saved.HasApiKey.Should().BeTrue();
        saved.AutonomousActionsEnabled.Should().BeTrue();

        string env = EnvText();
        env.Should().Contain("TRANSBRAIN_ASSISTANT_ENABLED=true")
            .And.Contain("TRANSBRAIN_ASSISTANT_PROVIDER=openrouter")
            .And.Contain("TRANSBRAIN_ASSISTANT_MODEL=anthropic/claude-sonnet-4.5")
            .And.Contain("TRANSBRAIN_ASSISTANT_AUTONOMY=true")
            .And.Contain("OPENROUTER_API_KEY=sk-or-test");

        AssistantSettingsResponse? read = await client
            .GetFromJsonAsync<AssistantSettingsResponse>("/api/system/assistant-settings");
        read.Should().BeEquivalentTo(saved);
    }

    [Fact]
    public async Task UpdateSettings_ResponseNeverContainsTheApiKey()
    {
        using WebApplicationFactory<Program> host = Provisioned();

        HttpResponseMessage response = await host.CreateSystemAdminClientFor()
            .PutAsJsonAsync("/api/system/assistant-settings", APayload(apiKey: "sk-or-very-secret"));

        string json = await response.Content.ReadAsStringAsync();
        json.Should().NotContain("sk-or-very-secret", "the key must never travel back to a browser");
    }

    [Fact]
    public async Task UpdateSettings_WithoutApiKey_KeepsStoredKey()
    {
        using WebApplicationFactory<Program> host = Provisioned();
        HttpClient client = host.CreateSystemAdminClientFor();
        await client.PutAsJsonAsync("/api/system/assistant-settings", APayload(apiKey: "sk-or-first"));

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/system/assistant-settings", APayload(model: "google/gemini-2.5-pro", apiKey: null));

        AssistantSettingsResponse? saved = await response.Content.ReadFromJsonAsync<AssistantSettingsResponse>();
        saved!.Model.Should().Be("google/gemini-2.5-pro");
        saved.HasApiKey.Should().BeTrue();
        EnvText().Should().Contain("OPENROUTER_API_KEY=sk-or-first");
    }

    [Fact]
    public async Task UpdateSettings_CustomProvider_WritesAndLaterRemovesBaseUrl()
    {
        using WebApplicationFactory<Program> host = Provisioned();
        HttpClient client = host.CreateSystemAdminClientFor();

        await client.PutAsJsonAsync("/api/system/assistant-settings", APayload(
            provider: "custom", model: "llama3", customBaseUrl: "http://ollama:11434/v1", apiKey: null));
        EnvText().Should().Contain("OPENAI_BASE_URL=http://ollama:11434/v1");

        // Back to a cloud provider: a stale base URL would silently hijack real OpenAI calls.
        await client.PutAsJsonAsync("/api/system/assistant-settings", APayload());
        EnvText().Should().NotContain("OPENAI_BASE_URL");
    }

    [Fact]
    public async Task UpdateSettings_PreservesForeignLinesInEnvFile()
    {
        await File.WriteAllLinesAsync(Path.Combine(_dataDirectory, ".env"),
            ["# hand-written note", "HERMES_TIMEZONE=Europe/Berlin"]);
        using WebApplicationFactory<Program> host = Provisioned();

        await host.CreateSystemAdminClientFor()
            .PutAsJsonAsync("/api/system/assistant-settings", APayload());

        string env = EnvText();
        env.Should().Contain("# hand-written note", "the volume is also edited by hand and by Hermes itself")
            .And.Contain("HERMES_TIMEZONE=Europe/Berlin")
            .And.Contain("TRANSBRAIN_ASSISTANT_ENABLED=true");
    }

    [Fact]
    public async Task UpdateSettings_UnknownProvider_ReturnsBadRequest()
    {
        using WebApplicationFactory<Program> host = Provisioned();

        HttpResponseMessage response = await host.CreateSystemAdminClientFor()
            .PutAsJsonAsync("/api/system/assistant-settings", APayload(provider: "skynet"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetStatus_ProvisionedAndConfigured_ReportsEnabledAndReady()
    {
        using WebApplicationFactory<Program> host = Provisioned();
        await host.CreateSystemAdminClientFor()
            .PutAsJsonAsync("/api/system/assistant-settings", APayload());

        HttpClient viewer = host.CreateClient();
        viewer.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "viewer");
        AssistantStatusResponse? status = await viewer
            .GetFromJsonAsync<AssistantStatusResponse>("/api/assistant/status");

        status!.Enabled.Should().BeTrue();
        status.Ready.Should().BeTrue();
    }
}

file static class FactoryExtensions
{
    /// <summary>Same shape as <see cref="TransBrainApiFactory.CreateSystemAdminClient"/>, for derived hosts.</summary>
    public static HttpClient CreateSystemAdminClientFor(this WebApplicationFactory<Program> host)
    {
        HttpClient client = host.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, "system-admin");
        client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, TestAuthHandler.NoTenant);
        return client;
    }
}
