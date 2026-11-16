using FluentValidation;

namespace TransBrain.Application.Features.Imports.CreateImportSession;

internal sealed class CreateApiImportSessionCommandValidator : AbstractValidator<CreateApiImportSessionCommand>
{
    private const int MaxUrlLength = 2_000;
    private const int MaxHeaders = 20;
    private const int MaxHeaderNameLength = 200;
    private const int MaxHeaderValueLength = 4_000;

    public CreateApiImportSessionCommandValidator()
    {
        RuleFor(c => c.Url)
            .NotEmpty()
            .MaximumLength(MaxUrlLength)
            .Must(url => CreateApiImportSessionCommandHandler.TryCreateHttpUrl(url, out _))
            .WithMessage("The URL must be an absolute http:// or https:// address.");

        RuleFor(c => c.Headers)
            .NotNull()
            .Must(headers => headers.Count <= MaxHeaders)
            .WithMessage($"At most {MaxHeaders} headers are allowed.");

        RuleForEach(c => c.Headers).ChildRules(header =>
        {
            header.RuleFor(h => h.Name)
                .NotEmpty()
                .MaximumLength(MaxHeaderNameLength)
                .Must(BeValidHeaderName)
                .WithMessage("Header names must be valid HTTP tokens without spaces or colons.");

            header.RuleFor(h => h.Value)
                .NotEmpty()
                .MaximumLength(MaxHeaderValueLength);
        });
    }

    /// <summary>RFC 9110 field-name token characters; keeps header injection out of the one request we send.</summary>
    private static bool BeValidHeaderName(string name)
        => !string.IsNullOrEmpty(name) && name.All(static c =>
            c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
                or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.'
                or '^' or '_' or '`' or '|' or '~');
}
