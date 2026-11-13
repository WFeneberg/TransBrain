using FluentValidation;

namespace TransBrain.Application.Features.Imports.CreateImportSession;

internal sealed class CreateFileImportSessionCommandValidator : AbstractValidator<CreateFileImportSessionCommand>
{
    public CreateFileImportSessionCommandValidator()
    {
        RuleFor(c => c.FileName)
            .NotEmpty()
            .Must(name => ImportFileTypes.Resolve(name) is not null)
            .WithMessage(_ =>
                $"Unsupported file type. Supported: {string.Join(", ", ImportFileTypes.SupportedExtensions.Order(StringComparer.Ordinal))}.");

        RuleFor(c => c.Content)
            .NotEmpty()
            .WithMessage("The file is empty.");

        RuleFor(c => c.Content.Length)
            .LessThanOrEqualTo(ImportLimits.MaxFileBytes)
            .WithMessage($"The file exceeds the maximum size of {ImportLimits.MaxFileBytes / (1024 * 1024)} MB.")
            .When(c => c.Content is not null);
    }
}
