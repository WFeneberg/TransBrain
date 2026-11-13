using FluentValidation;
using TransBrain.Application.Abstractions;

namespace TransBrain.Application.Features.Imports.CreateImportSession;

internal sealed class CreateDatabaseImportSessionCommandValidator
    : AbstractValidator<CreateDatabaseImportSessionCommand>
{
    private const int MaxConnectionStringLength = 4_000;

    public CreateDatabaseImportSessionCommandValidator()
    {
        RuleFor(c => c.Provider)
            .NotEmpty()
            .Must(p => Enum.TryParse<DatabaseImportProvider>(p, ignoreCase: true, out _))
            .WithMessage(_ =>
                $"Unknown provider. Supported: {string.Join(", ", Enum.GetNames<DatabaseImportProvider>())}.");

        RuleFor(c => c.ConnectionString)
            .NotEmpty()
            .MaximumLength(MaxConnectionStringLength);
    }
}
