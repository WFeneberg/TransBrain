using FluentValidation;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

/// <remarks>
/// Mirrors <see cref="DriverDocumentsInfo.Create"/> so all field problems are reported at once,
/// keyed "Documents.&lt;Field&gt;" by the parent validators.
/// </remarks>
internal sealed class DriverDocumentsDataValidator : AbstractValidator<DriverDocumentsData>
{
    public DriverDocumentsDataValidator()
    {
        RuleFor(d => d.LicenseNumber).MaximumLength(DriverDocumentsInfo.MaxLicenseNumberLength);
        RuleFor(d => d.IssuingAuthority).MaximumLength(DriverDocumentsInfo.MaxIssuingAuthorityLength);
        RuleFor(d => d.IssueDate)
            .Must(date => date!.Value.Year >= DriverDocumentsInfo.MinYear)
            .When(d => d.IssueDate is not null)
            .WithMessage($"The licence issue date must not be before {DriverDocumentsInfo.MinYear}.");
    }
}
