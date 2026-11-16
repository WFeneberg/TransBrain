using FluentValidation;

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
    }
}
