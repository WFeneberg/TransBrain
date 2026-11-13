using FluentValidation;

namespace TransBrain.Application.Features.Imports.ExecuteImport;

internal sealed class ExecuteImportCommandValidator : AbstractValidator<ExecuteImportCommand>
{
    public ExecuteImportCommandValidator()
    {
        RuleFor(c => c.TableName).NotEmpty();
        RuleFor(c => c.TargetEntity).NotEmpty();
        RuleFor(c => c.Mappings).NotEmpty();
        RuleForEach(c => c.Mappings)
            .Must(m => !string.IsNullOrWhiteSpace(m.TargetField))
            .WithMessage("Each mapping needs a target field.");
    }
}
