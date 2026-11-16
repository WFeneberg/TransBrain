using TransBrain.Domain.Common;

namespace TransBrain.Application.Abstractions;

/// <summary>
/// The assistant's runtime configuration. It deliberately does NOT live in the database: the
/// Hermes agent reads its provider keys and switches from the <c>.env</c> file in its persistent
/// volume, re-read at the start of every turn — so that file is the single source of truth, the
/// central administration UI writes it through this seam, and an operator may equally edit it by
/// hand (the volume is bind-mounted and partially checked into git, see deploy/hermes/README.md).
/// </summary>
public interface IAssistantConfigurationStore
{
    /// <summary>Never fails: an unprovisioned or empty volume loads as disabled defaults.</summary>
    Task<AssistantConfiguration> LoadAsync(CancellationToken cancellationToken);

    /// <summary>Persists the update and returns the configuration as re-read from storage.</summary>
    Task<Result<AssistantConfiguration>> SaveAsync(
        AssistantConfigurationUpdate update, CancellationToken cancellationToken);
}

/// <param name="Provisioned">False when no agent volume is wired up on this deployment at all.</param>
/// <param name="HasApiKey">Whether the selected provider's API key is present (never the key itself).</param>
public sealed record AssistantConfiguration(
    bool Provisioned,
    bool Enabled,
    string Provider,
    string Model,
    string? CustomBaseUrl,
    bool AutonomousActionsEnabled,
    bool HasApiKey);

/// <param name="ApiKey">New API key for the selected provider, or null to keep the stored one.</param>
public sealed record AssistantConfigurationUpdate(
    bool Enabled,
    string Provider,
    string Model,
    string? CustomBaseUrl,
    bool AutonomousActionsEnabled,
    string? ApiKey);
