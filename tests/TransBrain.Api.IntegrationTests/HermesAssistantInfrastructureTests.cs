using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Infrastructure.Assistant;

namespace TransBrain.Api.IntegrationTests;

/// <summary>
/// The two Hermes adapters below the endpoints: the .env store against a real directory and the
/// gateway client against a stub HTTP server.
/// </summary>
public sealed class HermesEnvAssistantConfigurationStoreTests : IDisposable
{
    private readonly string _dataDirectory = Directory.CreateTempSubdirectory("hermes-store-").FullName;

    public void Dispose() => Directory.Delete(_dataDirectory, recursive: true);

    private HermesEnvAssistantConfigurationStore Store(string? directory) => new(
        Options.Create(new HermesAgentOptions { DataDirectory = directory }),
        NullLogger<HermesEnvAssistantConfigurationStore>.Instance);

    private static AssistantConfigurationUpdate AnUpdate(
        string provider = "openrouter", string? apiKey = "sk-1", string? customBaseUrl = null) =>
        new(true, provider, "some/model", customBaseUrl, false, apiKey);

    [Fact]
    public async Task LoadAsync_NoDataDirectoryConfigured_IsNotProvisioned()
    {
        AssistantConfiguration configuration = await Store(null).LoadAsync(CancellationToken.None);

        configuration.Provisioned.Should().BeFalse();
        configuration.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_DirectoryMissing_IsNotProvisioned()
    {
        AssistantConfiguration configuration = await Store(Path.Combine(_dataDirectory, "gone"))
            .LoadAsync(CancellationToken.None);

        configuration.Provisioned.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_EmptyVolume_IsProvisionedButDisabled()
    {
        AssistantConfiguration configuration = await Store(_dataDirectory).LoadAsync(CancellationToken.None);

        configuration.Provisioned.Should().BeTrue();
        configuration.Enabled.Should().BeFalse();
        configuration.Provider.Should().Be("openrouter");
        configuration.HasApiKey.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_ParsesCommentsWhitespaceAndCase()
    {
        await File.WriteAllLinesAsync(Path.Combine(_dataDirectory, ".env"),
        [
            "# comment",
            "",
            "  TRANSBRAIN_ASSISTANT_ENABLED = true",
            "transbrain_assistant_provider=anthropic",
            "TRANSBRAIN_ASSISTANT_MODEL=claude-sonnet-4-5",
            "ANTHROPIC_API_KEY=sk-ant-1",
            "TRANSBRAIN_ASSISTANT_AUTONOMY=1"
        ]);

        AssistantConfiguration configuration = await Store(_dataDirectory).LoadAsync(CancellationToken.None);

        configuration.Enabled.Should().BeTrue();
        configuration.Provider.Should().Be("anthropic");
        configuration.Model.Should().Be("claude-sonnet-4-5");
        configuration.HasApiKey.Should().BeTrue();
        configuration.AutonomousActionsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_KeyOfAnotherProvider_DoesNotCountAsConfigured()
    {
        await File.WriteAllLinesAsync(Path.Combine(_dataDirectory, ".env"),
            ["TRANSBRAIN_ASSISTANT_PROVIDER=anthropic", "OPENROUTER_API_KEY=sk-or-1"]);

        AssistantConfiguration configuration = await Store(_dataDirectory).LoadAsync(CancellationToken.None);

        configuration.HasApiKey.Should().BeFalse("the selected provider's own key is what matters");
    }

    [Fact]
    public async Task SaveAsync_NoDataDirectory_FailsUnavailable()
    {
        Result<AssistantConfiguration> result = await Store(null)
            .SaveAsync(AnUpdate(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.NotProvisioned");
        result.Error.Type.Should().Be(ErrorType.Unavailable);
    }

    [Fact]
    public async Task SaveAsync_CreatesEnvAndReturnsReloadedConfiguration()
    {
        Result<AssistantConfiguration> result = await Store(_dataDirectory)
            .SaveAsync(AnUpdate(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Provisioned.Should().BeTrue();
        result.Value.Enabled.Should().BeTrue();
        result.Value.HasApiKey.Should().BeTrue();
        File.Exists(Path.Combine(_dataDirectory, ".env")).Should().BeTrue();
        File.Exists(Path.Combine(_dataDirectory, ".env.tmp")).Should().BeFalse("the write is temp-and-move");
    }

    [Fact]
    public async Task SaveAsync_UpsertsManagedKeysInPlacePreservingOrder()
    {
        await File.WriteAllLinesAsync(Path.Combine(_dataDirectory, ".env"),
            ["# keep me", "TRANSBRAIN_ASSISTANT_ENABLED=false", "HERMES_TIMEZONE=Europe/Berlin"]);

        await Store(_dataDirectory).SaveAsync(AnUpdate(), CancellationToken.None);

        string[] lines = await File.ReadAllLinesAsync(Path.Combine(_dataDirectory, ".env"));
        lines[0].Should().Be("# keep me");
        lines[1].Should().Be("TRANSBRAIN_ASSISTANT_ENABLED=true", "existing keys are updated in place");
        lines[2].Should().Be("HERMES_TIMEZONE=Europe/Berlin");
    }

    [Fact]
    public async Task SaveAsync_SwitchingProviders_KeepsBothKeysOnFile()
    {
        HermesEnvAssistantConfigurationStore store = Store(_dataDirectory)!;
        await store.SaveAsync(AnUpdate(provider: "openrouter", apiKey: "sk-or-1"), CancellationToken.None);

        Result<AssistantConfiguration> result = await store.SaveAsync(
            AnUpdate(provider: "anthropic", apiKey: "sk-ant-1"), CancellationToken.None);

        string env = await File.ReadAllTextAsync(Path.Combine(_dataDirectory, ".env"));
        env.Should().Contain("OPENROUTER_API_KEY=sk-or-1", "switching back must not cost the stored key")
            .And.Contain("ANTHROPIC_API_KEY=sk-ant-1");
        result.Value.Provider.Should().Be("anthropic");
        result.Value.HasApiKey.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_CustomProvider_WritesOpenAiKeyAndBaseUrl()
    {
        await Store(_dataDirectory).SaveAsync(
            AnUpdate(provider: "custom", apiKey: "sk-local", customBaseUrl: "http://ollama:11434/v1"),
            CancellationToken.None);

        string env = await File.ReadAllTextAsync(Path.Combine(_dataDirectory, ".env"));
        env.Should().Contain("OPENAI_API_KEY=sk-local", "Hermes reaches custom endpoints via its openai-api provider")
            .And.Contain("OPENAI_BASE_URL=http://ollama:11434/v1");
    }
}

/// <summary>The gateway client's error mapping, with a stub standing in for the Hermes container.</summary>
public sealed class HermesAgentClientTests
{
    private static HermesAgentClient Client(string baseUrl, string? apiKey = "test-key-123")
    {
        HttpClient http = new() { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
        if (apiKey is not null)
        {
            http.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
        }

        return new HermesAgentClient(http, NullLogger<HermesAgentClient>.Instance);
    }

    private static AssistantChatTurn ATurn() => new(
        [new AssistantTurnMessage("user", "Hallo")], "anthropic/claude-sonnet-4.5", "openrouter");

    private const string GoodCompletion =
        """{"choices":[{"message":{"role":"assistant","content":"Hallo! Wie kann ich helfen?"},"finish_reason":"stop"}]}""";

    [Fact]
    public async Task SendAsync_GatewayAnswers_ReturnsReplyAndSendsBearerKey()
    {
        using StubJsonApi stub = new(GoodCompletion);

        Result<AssistantReply> result = await Client(stub.BaseUrl).SendAsync(ATurn(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Hallo! Wie kann ich helfen?");
        stub.LastAuthorization.Should().Be("Bearer test-key-123");
    }

    [Fact]
    public async Task SendAsync_NoBaseAddress_ReportsNotProvisioned()
    {
        HermesAgentClient client = new(new HttpClient(), NullLogger<HermesAgentClient>.Instance);

        Result<AssistantReply> result = await client.SendAsync(ATurn(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.NotProvisioned");
    }

    [Fact]
    public async Task SendAsync_GatewayRefusesKey_ReportsAuthFailed()
    {
        using StubJsonApi stub = new("""{"error":"invalid key"}""", statusCode: 401);

        Result<AssistantReply> result = await Client(stub.BaseUrl, apiKey: "wrong")
            .SendAsync(ATurn(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.AuthFailed");
        result.Error.Type.Should().Be(ErrorType.Unavailable);
    }

    [Fact]
    public async Task SendAsync_GatewayErrors_ReportsUpstreamError()
    {
        using StubJsonApi stub = new("""{"error":"boom"}""", statusCode: 500);

        Result<AssistantReply> result = await Client(stub.BaseUrl).SendAsync(ATurn(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.UpstreamError");
    }

    [Fact]
    public async Task SendAsync_TurnFinishedWithError_ReportsUpstreamError()
    {
        using StubJsonApi stub = new(
            """{"choices":[{"message":{"role":"assistant","content":"…"},"finish_reason":"error"}]}""");

        Result<AssistantReply> result = await Client(stub.BaseUrl).SendAsync(ATurn(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.UpstreamError");
    }

    [Fact]
    public async Task SendAsync_NobodyListening_ReportsUnreachable()
    {
        // A port from the dynamic range nothing in this test run listens on.
        Result<AssistantReply> result = await Client("http://127.0.0.1:59999")
            .SendAsync(ATurn(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.Unreachable");
    }
}
