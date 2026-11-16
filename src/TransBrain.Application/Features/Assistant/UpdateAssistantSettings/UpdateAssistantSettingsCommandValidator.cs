using FluentValidation;

namespace TransBrain.Application.Features.Assistant.UpdateAssistantSettings;

public sealed class UpdateAssistantSettingsCommandValidator : AbstractValidator<UpdateAssistantSettingsCommand>
{
    public UpdateAssistantSettingsCommandValidator()
    {
        RuleFor(c => c.Payload).NotNull();

        When(c => c.Payload is not null, () =>
            RuleFor(c => c.Payload).SetValidator(new AssistantSettingsPayloadValidator()));
    }
}

internal sealed class AssistantSettingsPayloadValidator : AbstractValidator<AssistantSettingsPayload>
{
    public AssistantSettingsPayloadValidator()
    {
        RuleFor(p => p.Provider)
            .NotEmpty()
            .Must(AssistantProviders.IsKnown)
            .WithMessage($"Provider must be one of: {string.Join(", ", AssistantProviders.Known)}.");

        // The model travels into the agent's .env file and into provider requests: a slug, never
        // free text. Required only when the assistant is switched on, so an admin can park a
        // half-filled configuration while it is off.
        RuleFor(p => p.Model)
            .NotEmpty()
            .When(p => p.Enabled)
            .WithMessage("A model is required while the assistant is enabled.");

        RuleFor(p => p.Model)
            .MaximumLength(200)
            .Must(HaveNoInnerWhitespace)
            .WithMessage("The model id must not contain whitespace.");

        RuleFor(p => p.CustomBaseUrl)
            .NotEmpty()
            .When(p => string.Equals(p.Provider, AssistantProviders.Custom, StringComparison.OrdinalIgnoreCase))
            .WithMessage("A base URL is required for a custom OpenAI-compatible endpoint.");

        RuleFor(p => p.CustomBaseUrl)
            .MaximumLength(500)
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed)
                         && parsed.Scheme is "http" or "https")
            .When(p => !string.IsNullOrWhiteSpace(p.CustomBaseUrl))
            .WithMessage("The base URL must be an absolute http(s) URL.");

        RuleFor(p => p.ApiKey)
            .MaximumLength(500)
            .Must(HaveNoInnerWhitespace)
            .When(p => !string.IsNullOrWhiteSpace(p.ApiKey))
            .WithMessage("The API key must not contain whitespace.");
    }

    // These values are written as KEY=value lines into the agent's .env — inner whitespace (and
    // above all line breaks) would corrupt the file or smuggle in additional variables. The
    // handler trims, so surrounding whitespace is tolerated here.
    private static bool HaveNoInnerWhitespace(string? value) =>
        string.IsNullOrEmpty(value) || !value.Trim().Any(char.IsWhiteSpace);
}
