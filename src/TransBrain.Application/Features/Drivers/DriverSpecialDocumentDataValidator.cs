using FluentValidation;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

/// <remarks>Keyed "SpecialDocuments[i].&lt;Field&gt;" by the parent validators, so the form can mark the exact row.</remarks>
internal sealed class DriverSpecialDocumentDataValidator : AbstractValidator<DriverSpecialDocumentData>
{
    public DriverSpecialDocumentDataValidator()
    {
        RuleFor(d => d.Type).NotEmpty().MaximumLength(DriverSpecialDocument.MaxTypeLength);
        RuleFor(d => d.LastInspectionDate)
            .Must(date => date!.Value.Year >= DriverSpecialDocument.MinYear)
            .When(d => d.LastInspectionDate is not null)
            .WithMessage($"The last inspection date must not be before {DriverSpecialDocument.MinYear}.");
        RuleFor(d => d.ValidUntil)
            .Must(date => date!.Value.Year >= DriverSpecialDocument.MinYear)
            .When(d => d.ValidUntil is not null)
            .WithMessage($"The expiry date must not be before {DriverSpecialDocument.MinYear}.");
        RuleFor(d => d.Comment).MaximumLength(DriverSpecialDocument.MaxCommentLength);
    }
}
