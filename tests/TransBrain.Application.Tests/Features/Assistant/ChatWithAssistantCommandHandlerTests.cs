using AwesomeAssertions;
using TransBrain.Application.Abstractions;
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
        ChatWithAssistantCommandHandler handler = new(store, new FakeAssistantGateway(), new FakeAssistantFileDrop(), StubCurrentUser.Viewer());

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
        ChatWithAssistantCommandHandler handler = new(store, new FakeAssistantGateway(), new FakeAssistantFileDrop(), StubCurrentUser.Viewer());

        Result<AssistantChatResponse> result = await handler.Handle(ACommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.Disabled");
    }

    [Fact]
    public async Task Handle_EnabledButNoApiKey_ReturnsNotConfigured()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        store.Configuration = store.Configuration with { HasApiKey = false };
        ChatWithAssistantCommandHandler handler = new(store, new FakeAssistantGateway(), new FakeAssistantFileDrop(), StubCurrentUser.Viewer());

        Result<AssistantChatResponse> result = await handler.Handle(ACommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.NotConfigured");
    }

    [Fact]
    public async Task Handle_Ready_ReturnsReplyAndSendsConfiguredModel()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        FakeAssistantGateway gateway = new() { Reply = "So geht das." };
        ChatWithAssistantCommandHandler handler = new(store, gateway, new FakeAssistantFileDrop(), StubCurrentUser.Dispatcher());

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
        ChatWithAssistantCommandHandler handler = new(store, gateway, new FakeAssistantFileDrop(), StubCurrentUser.Dispatcher());

        await handler.Handle(ACommand(pageContext: "Touren / Plantafel"), CancellationToken.None);

        gateway.LastTurn!.Messages.Should().HaveCount(2);
        gateway.LastTurn.Messages[0].Role.Should().Be("system");
        gateway.LastTurn.Messages[0].Content.Should().Contain("dispatcher-sub")
            .And.Contain("disponent")
            .And.Contain("Touren / Plantafel");
        gateway.LastTurn.Messages[^1].Content.Should().Be("Wie lege ich einen Auftrag an?");
    }

    [Fact]
    public async Task Handle_UserWithoutWriteRole_SystemContextForbidsWritesEvenWithAutonomyOn()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        store.Configuration = store.Configuration with { AutonomousActionsEnabled = true };
        FakeAssistantGateway gateway = new();
        ChatWithAssistantCommandHandler handler = new(store, gateway, new FakeAssistantFileDrop(), StubCurrentUser.Viewer());

        await handler.Handle(ACommand(), CancellationToken.None);

        gateway.LastTurn!.Messages[0].Content.Should().Contain("NICHT erlaubt",
            "the user's own role is the hard gate, not the autonomy switch");
    }

    [Fact]
    public async Task Handle_WriteRoleAutonomyOff_SystemContextAllowsInstructedWrites()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        FakeAssistantGateway gateway = new();
        ChatWithAssistantCommandHandler handler = new(store, gateway, new FakeAssistantFileDrop(), StubCurrentUser.Dispatcher());

        await handler.Handle(ACommand(), CancellationToken.None);

        gateway.LastTurn!.Messages[0].Content.Should().Contain("Schreibaktionen auf Anweisung sind erlaubt",
            "a dispatcher's explicit instruction must not be blocked by the autonomy switch")
            .And.NotContain("NICHT erlaubt");
    }

    [Fact]
    public async Task Handle_AutonomyOn_SystemContextAllowsWriteActions()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        store.Configuration = store.Configuration with { AutonomousActionsEnabled = true };
        FakeAssistantGateway gateway = new();
        ChatWithAssistantCommandHandler handler = new(store, gateway, new FakeAssistantFileDrop(), StubCurrentUser.Dispatcher());

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
        ChatWithAssistantCommandHandler handler = new(store, gateway, new FakeAssistantFileDrop(), StubCurrentUser.Admin());

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
        ChatWithAssistantCommandHandler handler = new(store, gateway, new FakeAssistantFileDrop(), StubCurrentUser.Viewer());

        Result<AssistantChatResponse> result = await handler.Handle(ACommand(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.Unreachable");
    }

    [Fact]
    public async Task Handle_ImageAttachment_RidesInlineOnLastUserMessage()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        FakeAssistantGateway gateway = new();
        FakeAssistantFileDrop fileDrop = new();
        ChatWithAssistantCommandHandler handler = new(store, gateway, fileDrop, StubCurrentUser.Viewer());
        ChatWithAssistantCommand command = new(new AssistantChatPayload(
            [new AssistantChatMessagePayload("user", "Was zeigt dieses Bild?")],
            null,
            [new AssistantAttachmentPayload("foto.png", "image/png", Convert.ToBase64String([1, 2, 3]))]));

        Result<AssistantChatResponse> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fileDrop.SavedFiles.Should().BeEmpty("images go inline, not into the volume");
        AssistantTurnMessage lastMessage = gateway.LastTurn!.Messages[^1];
        lastMessage.Images.Should().ContainSingle()
            .Which.Should().Be(new AssistantImageAttachment("image/png", Convert.ToBase64String(new byte[] { 1, 2, 3 })));
        lastMessage.Content.Should().Be("Was zeigt dieses Bild?");
    }

    [Fact]
    public async Task Handle_FileAttachment_DropsIntoVolumeAndTellsAgentThePath()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        FakeAssistantGateway gateway = new();
        FakeAssistantFileDrop fileDrop = new();
        ChatWithAssistantCommandHandler handler = new(store, gateway, fileDrop, StubCurrentUser.Viewer());
        byte[] content = "Rechnung 4711"u8.ToArray();
        ChatWithAssistantCommand command = new(new AssistantChatPayload(
            [new AssistantChatMessagePayload("user", "Prüfe die Rechnung.")],
            null,
            [new AssistantAttachmentPayload("rechnung.pdf", "application/pdf", Convert.ToBase64String(content))]));

        Result<AssistantChatResponse> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fileDrop.SavedFiles.Should().ContainSingle();
        fileDrop.SavedFiles[0].FileName.Should().Be("rechnung.pdf");
        fileDrop.SavedFiles[0].Content.Should().Equal(content);
        AssistantTurnMessage lastMessage = gateway.LastTurn!.Messages[^1];
        lastMessage.Content.Should().StartWith("Prüfe die Rechnung.")
            .And.Contain("/opt/data/uploads/test/rechnung.pdf");
        lastMessage.Images.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MixedAttachments_SplitsImagesFromFiles()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        FakeAssistantGateway gateway = new();
        FakeAssistantFileDrop fileDrop = new();
        ChatWithAssistantCommandHandler handler = new(store, gateway, fileDrop, StubCurrentUser.Viewer());
        ChatWithAssistantCommand command = new(new AssistantChatPayload(
            [new AssistantChatMessagePayload("user", "Sieh dir beides an.")],
            null,
            [
                new AssistantAttachmentPayload("skizze.jpg", "image/jpeg", Convert.ToBase64String([9])),
                new AssistantAttachmentPayload("liste.csv", "text/csv", Convert.ToBase64String([7]))
            ]));

        await handler.Handle(command, CancellationToken.None);

        fileDrop.SavedFiles.Should().ContainSingle().Which.FileName.Should().Be("liste.csv");
        gateway.LastTurn!.Messages[^1].Images.Should().ContainSingle()
            .Which.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task Handle_FileDropFails_PropagatesError()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        FakeAssistantGateway gateway = new();
        FakeAssistantFileDrop fileDrop = new()
        {
            Failure = Error.Unavailable("Assistant.VolumeNotWritable", "read-only")
        };
        ChatWithAssistantCommandHandler handler = new(store, gateway, fileDrop, StubCurrentUser.Viewer());
        ChatWithAssistantCommand command = new(new AssistantChatPayload(
            [new AssistantChatMessagePayload("user", "Prüfe die Datei.")],
            null,
            [new AssistantAttachmentPayload("daten.txt", "text/plain", Convert.ToBase64String([1]))]));

        Result<AssistantChatResponse> result = await handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.VolumeNotWritable");
        gateway.LastTurn.Should().BeNull("the turn must not reach the agent when the handover failed");
    }
}
