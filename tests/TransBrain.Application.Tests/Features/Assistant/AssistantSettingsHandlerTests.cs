using AwesomeAssertions;
using TransBrain.Application.Features.Assistant;
using TransBrain.Application.Features.Assistant.GetAssistantSettings;
using TransBrain.Application.Features.Assistant.GetAssistantStatus;
using TransBrain.Application.Features.Assistant.UpdateAssistantSettings;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Assistant;

public class AssistantSettingsHandlerTests
{
    [Fact]
    public async Task Handle_GetStatus_UnprovisionedIsDisabledAndNotReady()
    {
        GetAssistantStatusQueryHandler handler = new(new InMemoryAssistantConfigurationStore());

        Result<AssistantStatusResponse> result = await handler.Handle(
            new GetAssistantStatusQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Enabled.Should().BeFalse();
        result.Value.Ready.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_GetStatus_ProvisionedAndConfiguredIsReady()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        GetAssistantStatusQueryHandler handler = new(store);

        Result<AssistantStatusResponse> result = await handler.Handle(
            new GetAssistantStatusQuery(), CancellationToken.None);

        result.Value.Enabled.Should().BeTrue();
        result.Value.Ready.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_GetStatus_EnabledWithoutKeyIsEnabledButNotReady()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        store.Configuration = store.Configuration with { HasApiKey = false };
        GetAssistantStatusQueryHandler handler = new(store);

        Result<AssistantStatusResponse> result = await handler.Handle(
            new GetAssistantStatusQuery(), CancellationToken.None);

        result.Value.Enabled.Should().BeTrue();
        result.Value.Ready.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_GetSettings_ReturnsConfigurationWithoutApiKeyButWithProviderList()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        GetAssistantSettingsQueryHandler handler = new(store);

        Result<AssistantSettingsResponse> result = await handler.Handle(
            new GetAssistantSettingsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Provider.Should().Be("openrouter");
        result.Value.Model.Should().Be("anthropic/claude-sonnet-4.5");
        result.Value.HasApiKey.Should().BeTrue();
        result.Value.KnownProviders.Should().Contain(["openrouter", "custom"]);
    }

    [Fact]
    public async Task Handle_UpdateSettings_NormalizesAndSaves()
    {
        InMemoryAssistantConfigurationStore store = new();
        UpdateAssistantSettingsCommandHandler handler = new(store);

        Result<AssistantSettingsResponse> result = await handler.Handle(
            new UpdateAssistantSettingsCommand(new AssistantSettingsPayload(
                Enabled: true,
                Provider: "OpenRouter",
                Model: " anthropic/claude-sonnet-4.5 ",
                CustomBaseUrl: "  ",
                AutonomousActionsEnabled: true,
                ApiKey: " sk-or-123 ")),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.LastUpdate!.Provider.Should().Be("openrouter");
        store.LastUpdate.Model.Should().Be("anthropic/claude-sonnet-4.5");
        store.LastUpdate.CustomBaseUrl.Should().BeNull();
        store.LastUpdate.ApiKey.Should().Be("sk-or-123");
        result.Value.Enabled.Should().BeTrue();
        result.Value.AutonomousActionsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UpdateSettings_EmptyApiKeyKeepsStoredKey()
    {
        InMemoryAssistantConfigurationStore store = new InMemoryAssistantConfigurationStore().ProvisionedAndReady();
        UpdateAssistantSettingsCommandHandler handler = new(store);

        Result<AssistantSettingsResponse> result = await handler.Handle(
            new UpdateAssistantSettingsCommand(new AssistantSettingsPayload(
                true, "openrouter", "google/gemini-2.5-pro", null, false, ApiKey: "")),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        store.LastUpdate!.ApiKey.Should().BeNull("an empty key means 'keep the stored one'");
        result.Value.HasApiKey.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UpdateSettings_StoreFailurePassesThrough()
    {
        InMemoryAssistantConfigurationStore store = new()
        {
            SaveFailure = Error.Unavailable("Assistant.VolumeNotWritable", "read-only")
        };
        UpdateAssistantSettingsCommandHandler handler = new(store);

        Result<AssistantSettingsResponse> result = await handler.Handle(
            new UpdateAssistantSettingsCommand(new AssistantSettingsPayload(
                true, "openrouter", "m", null, false, null)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Assistant.VolumeNotWritable");
    }
}
