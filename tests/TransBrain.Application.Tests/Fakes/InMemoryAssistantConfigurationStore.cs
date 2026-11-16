using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Fakes;

/// <summary>Holds the configuration in memory; starts unprovisioned like a deployment without the agent.</summary>
public sealed class InMemoryAssistantConfigurationStore : IAssistantConfigurationStore
{
    public AssistantConfiguration Configuration { get; set; } = new(
        Provisioned: false,
        Enabled: false,
        Provider: "openrouter",
        Model: string.Empty,
        CustomBaseUrl: null,
        AutonomousActionsEnabled: false,
        HasApiKey: false);

    /// <summary>Set to make every save answer this error, as an unwritable volume would.</summary>
    public Error? SaveFailure { get; set; }

    public AssistantConfigurationUpdate? LastUpdate { get; private set; }

    public Task<AssistantConfiguration> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Configuration);

    public Task<Result<AssistantConfiguration>> SaveAsync(
        AssistantConfigurationUpdate update, CancellationToken cancellationToken)
    {
        if (SaveFailure is not null)
        {
            return Task.FromResult(Result<AssistantConfiguration>.Failure(SaveFailure));
        }

        LastUpdate = update;
        Configuration = new AssistantConfiguration(
            Provisioned: true,
            update.Enabled,
            update.Provider,
            update.Model,
            update.CustomBaseUrl,
            update.AutonomousActionsEnabled,
            HasApiKey: update.ApiKey is not null || Configuration.HasApiKey);
        return Task.FromResult(Result<AssistantConfiguration>.Success(Configuration));
    }

    public InMemoryAssistantConfigurationStore ProvisionedAndReady(
        string provider = "openrouter", string model = "anthropic/claude-sonnet-4.5")
    {
        Configuration = Configuration with
        {
            Provisioned = true,
            Enabled = true,
            Provider = provider,
            Model = model,
            HasApiKey = true
        };
        return this;
    }
}
