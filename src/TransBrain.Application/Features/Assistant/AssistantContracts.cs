using TransBrain.Application.Abstractions;

namespace TransBrain.Application.Features.Assistant;

/// <summary>What the central administration sees. Never contains the API key itself.</summary>
public sealed record AssistantSettingsResponse(
    bool Provisioned,
    bool Enabled,
    string Provider,
    string Model,
    string? CustomBaseUrl,
    bool AutonomousActionsEnabled,
    bool HasApiKey,
    IReadOnlyList<string> KnownProviders)
{
    public static AssistantSettingsResponse From(AssistantConfiguration configuration) => new(
        configuration.Provisioned,
        configuration.Enabled,
        configuration.Provider,
        configuration.Model,
        configuration.CustomBaseUrl,
        configuration.AutonomousActionsEnabled,
        configuration.HasApiKey,
        AssistantProviders.Known);
}

/// <param name="ApiKey">Null or empty keeps the key already stored in the agent's volume.</param>
public sealed record AssistantSettingsPayload(
    bool Enabled,
    string Provider,
    string Model,
    string? CustomBaseUrl,
    bool AutonomousActionsEnabled,
    string? ApiKey);

/// <summary>What every signed-in user may know: whether the chat should be offered at all.</summary>
public sealed record AssistantStatusResponse(bool Enabled, bool Ready);

public sealed record AssistantChatMessagePayload(string Role, string Content);

/// <param name="DataBase64">Raw file bytes, base64-encoded (no data-URL prefix).</param>
public sealed record AssistantAttachmentPayload(string FileName, string ContentType, string DataBase64);

/// <summary>Shared bounds for chat attachments; the validator enforces them, the frontends mirror them.</summary>
public static class AssistantAttachmentLimits
{
    public const int MaxCount = 5;
    public const int MaxTotalBytes = 10 * 1024 * 1024;
    public const int MaxFileNameLength = 150;
}

/// <param name="PageContext">The route/title the user is currently on, so answers can fit the screen.</param>
/// <param name="Attachments">Files or images the user attached to the latest message, if any.</param>
public sealed record AssistantChatPayload(
    IReadOnlyList<AssistantChatMessagePayload> Messages,
    string? PageContext,
    IReadOnlyList<AssistantAttachmentPayload>? Attachments = null);

public sealed record AssistantChatResponse(string Reply);
