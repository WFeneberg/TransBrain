using System.Text;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Assistant.ChatWithAssistant;

internal sealed class ChatWithAssistantCommandHandler(
    IAssistantConfigurationStore store,
    IAssistantGateway gateway,
    IAssistantFileDrop fileDrop,
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

        Result<AssistantChatResponse>? attachmentFailure = await ApplyAttachmentsAsync(
            command.Payload, messages, cancellationToken);
        if (attachmentFailure is not null)
        {
            return attachmentFailure.Value;
        }

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

    /// <summary>
    /// Folds attachments into the latest user message: images ride inline (the gateway accepts
    /// multimodal content parts), other files land in the agent's volume and the message tells
    /// the agent where — its own file tools take it from there, the Hermes API itself has no
    /// file upload.
    /// </summary>
    private async Task<Result<AssistantChatResponse>?> ApplyAttachmentsAsync(
        AssistantChatPayload payload,
        List<AssistantTurnMessage> messages,
        CancellationToken cancellationToken)
    {
        if (payload.Attachments is not { Count: > 0 } attachments)
        {
            return null;
        }

        List<AssistantImageAttachment> images = [];
        List<AssistantFileToDrop> files = [];
        foreach (AssistantAttachmentPayload attachment in attachments)
        {
            if (attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                images.Add(new AssistantImageAttachment(attachment.ContentType, attachment.DataBase64));
            }
            else
            {
                // Base64 validity is guaranteed by the request validator.
                files.Add(new AssistantFileToDrop(attachment.FileName, Convert.FromBase64String(attachment.DataBase64)));
            }
        }

        string content = messages[^1].Content;

        if (files.Count > 0)
        {
            Result<IReadOnlyList<AssistantDroppedFile>> dropped = await fileDrop.SaveAsync(files, cancellationToken);
            if (!dropped.IsSuccess)
            {
                return Result<AssistantChatResponse>.Failure(dropped.Error!);
            }

            StringBuilder note = new(content);
            note.AppendLine();
            note.AppendLine();
            note.AppendLine("[Hinweis an den Assistenten: Der Benutzer hat dieser Nachricht Dateien angehängt. " +
                            "Sie liegen in deinem Container; lies sie bei Bedarf mit deinen Datei-Werkzeugen:]");
            foreach (AssistantDroppedFile file in dropped.Value)
            {
                note.AppendLine($"- {file.FileName}: {file.AgentPath}");
            }
            content = note.ToString();
        }

        messages[^1] = messages[^1] with
        {
            Content = content,
            Images = images.Count > 0 ? images : null
        };
        return null;
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

        // Write policy: the user's own role decides whether instructed writes are allowed at all;
        // the autonomy switch only adds self-initiated actions on top. The hard boundary stays the
        // service account's role and tenant, whatever the prompt says.
        bool mayInstructWrites = currentUser.IsInRole("admin") || currentUser.IsInRole("disponent");
        if (!mayInstructWrites)
        {
            context.AppendLine(
                "- Schreibende API-Aufrufe sind für diesen Benutzer NICHT erlaubt (Rolle ohne Schreibrechte): " +
                "führe keine aus, sondern erkläre die nötigen Schritte in der Oberfläche.");
        }
        else if (configuration.AutonomousActionsEnabled)
        {
            context.AppendLine(
                "- Autonome Aktionen sind erlaubt: Schreibaktionen darfst du über den Skill transbrain-api " +
                "ausführen — beauftragte sofort, sinnvolle eigene Vorschläge auch ohne Einzelanweisung; " +
                "kündige jede vorher kurz an.");
        }
        else
        {
            context.AppendLine(
                "- Schreibaktionen auf Anweisung sind erlaubt: was dieser Benutzer in der Unterhaltung klar " +
                "beauftragt (z. B. \"lege an\", \"ändere\", \"storniere\"), führst du über den Skill " +
                "transbrain-api aus; kündige es vorher kurz an. Nicht beauftragte Änderungen schlägst du " +
                "nur vor, statt sie auszuführen.");
        }

        // The service account can do more than a dispatcher; the prompt pins the agent to the
        // instructing user's own scope.
        if (mayInstructWrites && !currentUser.IsInRole("admin"))
        {
            context.AppendLine(
                "- Rollengrenze: Der Benutzer ist Disponent. Führe nur dispositive Schreibaktionen aus " +
                "(Aufträge, Touren, Zuweisungen) — keine Stammdaten (Fahrzeuge, Fahrer, Kunden, Kataloge) " +
                "und keine Einstellungen.");
        }

        return context.ToString();
    }
}
