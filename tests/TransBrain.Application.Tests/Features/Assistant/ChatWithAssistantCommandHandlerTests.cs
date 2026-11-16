using AwesomeAssertions;
using TransBrain.Application.Features.Assistant;
using TransBrain.Application.Features.Assistant.ChatWithAssistant;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Assistant;

public class ChatWithAssistantCommandHandlerTests
{
    private static ChatWithAssistantCommand ACommand(string? pageContext = null) => new(
        new AssistantChatPayload(
            [new AssistantChatMessagePayload("user", "Wie lege ich einen Auftrag an?")],
            pageContext));

    [Fact]
    public async Task Handle_NotProvisioned_ReturnsUnavailable()
    {
        InMemoryAssistantConfigurationStore store = new();
        ChatWithAssistantCommandHandler handler = new(store, new FakeAssistantGateway(), StubCurrentUser.Viewer());

        Result<AssistantChatResponse> result = await handler.Handle(ACommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.NotProvisioned");
        result.Error.Type.Should().Be(ErrorType.Unavailable);
    }

    [Fact]
    public async Task Handle_DisabledByAdministration_ReturnsUnavailable()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        store.Configuration = store.Configuration with { Enabled = false };
        ChatWithAssistantCommandHandler handler = new(store, new FakeAssistantGateway(), StubCurrentUser.Viewer());

        Result<AssistantChatResponse> result = await handler.Handle(ACommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.Disabled");
    }

    [Fact]
    public async Task Handle_EnabledButNoApiKey_ReturnsNotConfigured()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        store.Configuration = store.Configuration with { HasApiKey = false };
        ChatWithAssistantCommandHandler handler = new(store, new FakeAssistantGateway(), StubCurrentUser.Viewer());

        Result<AssistantChatResponse> result = await handler.Handle(ACommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.NotConfigured");
    }

    [Fact]
    public async Task Handle_Ready_ReturnsReplyAndSendsConfiguredModel()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        FakeAssistantGateway gateway = new() { Reply = "So geht das." };
        ChatWithAssistantCommandHandler handler = new(store, gateway, StubCurrentUser.Dispatcher());

        Result<AssistantChatResponse> result = await handler.Handle(ACommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Reply.Should().Be("So geht das.");
        gateway.LastTurn!.Model.Should().Be("anthropic/claude-sonnet-4.5");
        gateway.LastTurn.Provider.Should().Be("openrouter");
    }

    [Fact]
    public async Task Handle_Ready_PrependsSystemContextWithUserRolesAndPage()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        FakeAssistantGateway gateway = new();
        ChatWithAssistantCommandHandler handler = new(store, gateway, StubCurrentUser.Dispatcher());

        await handler.Handle(ACommand(pageContext: "Touren / Plantafel"), CancellationToken.None);

        gateway.LastTurn!.Messages.Should().HaveCount(2);
        gateway.LastTurn.Messages[0].Role.Should().Be("system");
        gateway.LastTurn.Messages[0].Content.Should().Contain("dispatcher-sub")
            .And.Contain("disponent")
            .And.Contain("Touren / Plantafel");
        gateway.LastTurn.Messages[^1].Content.Should().Be("Wie lege ich einen Auftrag an?");
    }

    [Fact]
    public async Task Handle_AutonomyOff_SystemContextForbidsWriteActions()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        FakeAssistantGateway gateway = new();
        ChatWithAssistantCommandHandler handler = new(store, gateway, StubCurrentUser.Viewer());

        await handler.Handle(ACommand(), CancellationToken.None);

        gateway.LastTurn!.Messages[0].Content.Should().Contain("NICHT erlaubt");
    }

    [Fact]
    public async Task Handle_AutonomyOn_SystemContextAllowsWriteActions()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        store.Configuration = store.Configuration with { AutonomousActionsEnabled = true };
        FakeAssistantGateway gateway = new();
        ChatWithAssistantCommandHandler handler = new(store, gateway, StubCurrentUser.Dispatcher());

        await handler.Handle(ACommand(), CancellationToken.None);

        gateway.LastTurn!.Messages[0].Content.Should().Contain("Autonome Aktionen sind erlaubt");
    }

    [Fact]
    public async Task Handle_CustomProvider_TranslatesToOpenAiApiProvider()
    {
        InMemoryAssistantConfigurationStore store =
            new InMemoryAssistantConfigurationStore().ProvisionedAndReady(provider: "custom", model: "llama3");
        store.Configuration = store.Configuration with { HasApiKey = false, CustomBaseUrl = "http://ollama:11434/v1" };
        FakeAssistantGateway gateway = new();
        ChatWithAssistantCommandHandler handler = new(store, gateway, StubCurrentUser.Admin());

        Result<AssistantChatResponse> result = await handler.Handle(ACommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("a custom endpoint may be keyless, like a local Ollama");
        gateway.LastTurn!.Provider.Should().Be("openai-api");
    }

    [Fact]
    public async Task Handle_GatewayFails_PassesErrorThrough()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        FakeAssistantGateway gateway = new()
        {
            Failure = Error.Unavailable("Assistant.Unreachable", "down")
        };
        ChatWithAssistantCommandHandler handler = new(store, gateway, StubCurrentUser.Viewer());

        Result<AssistantChatResponse> result = await handler.Handle(ACommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.Unreachable");
    }
}
