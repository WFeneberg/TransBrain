using FluentValidation;

namespace TransBrain.Application.Features.Imports.PreviewImport;

internal sealed class PreviewImportQueryValidator : AbstractValidator<PreviewImportQuery>
{
    public PreviewImportQueryValidator()
    {
        RuleFor(q => q.TableName).NotEmpty();
        RuleFor(q => q.TargetEntity).NotEmpty();
        RuleFor(q => q.Mappings).NotEmpty();
        RuleForEach(q => q.Mappings)
            .Must(m => !string.IsNullOrWhiteSpace(m.TargetField))
            .WithMessage("Each mapping needs a target field.");
        RuleFor(q => q.MaxRows).InclusiveBetween(1, 200);
    }
}
