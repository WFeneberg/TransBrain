using FluentValidation;
using System.Buffers.Text;

namespace TransBrain.Application.Features.Assistant.ChatWithAssistant;

public sealed class ChatWithAssistantCommandValidator : AbstractValidator<ChatWithAssistantCommand>
{
    public ChatWithAssistantCommandValidator()
    {
        RuleFor(c => c.Payload).NotNull();

        When(c => c.Payload is not null, () =>
            RuleFor(c => c.Payload).SetValidator(new AssistantChatPayloadValidator()));
    }
}

internal sealed class AssistantChatPayloadValidator : AbstractValidator<AssistantChatPayload>
{
    private static readonly string[] AllowedRoles = ["user", "assistant"];

    public AssistantChatPayloadValidator()
    {
        RuleFor(p => p.Messages)
            .NotEmpty()
            .WithMessage("At least one message is required.");

        When(p => p.Messages is { Count: > 0 }, () =>
        {
            // The frontend resends the whole conversation each turn; the cap bounds payload size
            // (a UI trims locally before it is ever hit), the role check keeps a client from
            // smuggling its own system message past the server-built context.
            RuleFor(p => p.Messages)
                .Must(m => m.Count <= 50)
                .WithMessage("A conversation may carry at most 50 messages per request.")
                .Must(m => m[^1].Role == "user")
                .WithMessage("The last message must come from the user.");

            RuleForEach(p => p.Messages).ChildRules(message =>
            {
                message.RuleFor(m => m.Role)
                    .Must(role => AllowedRoles.Contains(role))
                    .WithMessage("Message role must be 'user' or 'assistant'.");

                message.RuleFor(m => m.Content)
                    .NotEmpty()
                    .MaximumLength(8000);
            });
        });

        RuleFor(p => p.PageContext)
            .MaximumLength(300);

        When(p => p.Attachments is { Count: > 0 }, () =>
        {
            RuleFor(p => p.Attachments!)
                .Must(a => a.Count <= AssistantAttachmentLimits.MaxCount)
                .WithMessage($"At most {AssistantAttachmentLimits.MaxCount} attachments per message.")
                .Must(WithinTotalSize)
                .WithMessage($"Attachments may total at most {AssistantAttachmentLimits.MaxTotalBytes / (1024 * 1024)} MB.");

            RuleForEach(p => p.Attachments!).ChildRules(attachment =>
            {
                attachment.RuleFor(a => a.FileName)
                    .NotEmpty()
                    .MaximumLength(AssistantAttachmentLimits.MaxFileNameLength);

                attachment.RuleFor(a => a.ContentType)
                    .NotEmpty()
                    .MaximumLength(120);

                attachment.RuleFor(a => a.DataBase64)
                    .NotEmpty()
                    .Must(data => Base64.IsValid(data))
                    .WithMessage("Attachment content must be valid base64.");
            });
        });
    }

    /// <summary>Estimates decoded size from base64 length; exact enough for a request bound.</summary>
    private static bool WithinTotalSize(IReadOnlyList<AssistantAttachmentPayload> attachments) =>
        attachments.Sum(a => (long)(a.DataBase64?.Length ?? 0)) * 3 / 4 <= AssistantAttachmentLimits.MaxTotalBytes;
}
