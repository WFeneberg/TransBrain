using System.Text;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Assistant.ChatWithAssistant;

internal sealed class ChatWithAssistantCommandHandler(
    IAssistantConfigurationStore store,
    IAssistantGateway gateway,
    ICurrentUser currentUser)
    : ICommandHandler<ChatWithAssistantCommand, AssistantChatResponse>
{
    /// <summary>Realm roles worth telling the agent about, in rank order.</summary>
    private static readonly string[] KnownRoles = ["admin", "disponent", "fahrer", "viewer", "system-admin"];

    public async Task<Result<AssistantChatResponse>> Handle(
        ChatWithAssistantCommand command,
        CancellationToken cancellationToken)
    {
        AssistantConfiguration configuration = await store.LoadAsync(cancellationToken);

        if (!configuration.Provisioned)
        {
            return Error.Unavailable(
                "Assistant.NotProvisioned",
                "No AI assistant is provisioned on this installation.");
        }

        if (!configuration.Enabled)
        {
            return Error.Unavailable(
                "Assistant.Disabled",
                "The AI assistant is switched off in the central administration.");
        }

        if (!AssistantProviders.IsReadyForChat(configuration))
        {
            return Error.Unavailable(
                "Assistant.NotConfigured",
                "The AI assistant is missing a model or an API key; configure it in the central administration.");
        }

        // The Hermes gateway is stateless per request: the frontend carries the conversation and
        // this system message carries who is asking and where they are in the application.
        List<AssistantTurnMessage> messages = new(command.Payload.Messages.Count + 1)
        {
            new AssistantTurnMessage("system", BuildSystemContext(command.Payload, configuration))
        };
        messages.AddRange(command.Payload.Messages.Select(m => new AssistantTurnMessage(m.Role, m.Content)));

        // "custom" is the UI's name for any OpenAI-compatible endpoint; Hermes reaches it through
        // its openai-api provider whose base URL the configuration store wrote to the volume.
        string provider = string.Equals(configuration.Provider, AssistantProviders.Custom, StringComparison.OrdinalIgnoreCase)
            ? "openai-api"
            : configuration.Provider;

        Result<AssistantReply> reply = await gateway.SendAsync(
            new AssistantChatTurn(messages, configuration.Model, provider),
            cancellationToken);

        return reply.IsSuccess
            ? new AssistantChatResponse(reply.Value.Content)
            : Result<AssistantChatResponse>.Failure(reply.Error!);
    }

    private string BuildSystemContext(AssistantChatPayload payload, AssistantConfiguration configuration)
    {
        string roles = string.Join(", ", KnownRoles.Where(currentUser.IsInRole));

        StringBuilder context = new();
        context.AppendLine("Kontext dieser Anfrage aus der Speditionssoftware TransBrain:");
        context.AppendLine($"- Angemeldeter Benutzer: {currentUser.UserName ?? "unbekannt"}" +
                           (roles.Length > 0 ? $" (Rollen: {roles})" : string.Empty));

        if (!string.IsNullOrWhiteSpace(payload.PageContext))
        {
            context.AppendLine($"- Aktuell geöffnete Seite: {payload.PageContext}");
        }

        context.AppendLine(configuration.AutonomousActionsEnabled
            ? "- Autonome Aktionen sind erlaubt: klar beauftragte Schreibaktionen darfst du über den Skill transbrain-api selbst ausführen; kündige sie vorher kurz an."
            : "- Autonome Aktionen sind NICHT erlaubt: führe keine schreibenden API-Aufrufe aus, sondern erkläre die nötigen Schritte in der Oberfläche.");

        return context.ToString();
    }
}
