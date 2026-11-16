using TransBrain.Domain.Common;

namespace TransBrain.Application.Abstractions;

/// <summary>
/// The AI assistant behind the chat: one complete turn in, one reply out. Implemented against
/// the Hermes Agent gateway's OpenAI-compatible API; the Application layer only knows this seam.
/// </summary>
public interface IAssistantGateway
{
    Task<Result<AssistantReply>> SendAsync(AssistantChatTurn turn, CancellationToken cancellationToken);
}

/// <param name="Messages">Full conversation including the system message; the gateway is stateless.</param>
/// <param name="Model">Model id in the provider's naming, or null for the agent's configured default.</param>
/// <param name="Provider">Hermes provider id (for example <c>openrouter</c>), or null for the default.</param>
public sealed record AssistantChatTurn(
    IReadOnlyList<AssistantTurnMessage> Messages,
    string? Model,
    string? Provider);

/// <param name="Images">Inline images for multimodal models, only ever set on the latest user message.</param>
public sealed record AssistantTurnMessage(
    string Role,
    string Content,
    IReadOnlyList<AssistantImageAttachment>? Images = null);

/// <param name="DataBase64">Raw image bytes, base64-encoded; the gateway client builds the data URL.</param>
public sealed record AssistantImageAttachment(string ContentType, string DataBase64);

public sealed record AssistantReply(string Content);
