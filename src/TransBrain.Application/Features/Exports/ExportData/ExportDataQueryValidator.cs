using FluentValidation;

namespace TransBrain.Application.Features.Exports.ExportData;

internal sealed class ExportDataQueryValidator : AbstractValidator<ExportDataQuery>
{
    public ExportDataQueryValidator()
    {
        RuleFor(q => q.TargetEntity).NotEmpty();
        RuleFor(q => q.Format).NotEmpty();
    }
}
