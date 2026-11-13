using FluentValidation;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

/// <remarks>Keyed "Relatives[i].&lt;Field&gt;" by the parent validators, so the form can mark the exact row.</remarks>
internal sealed class DriverRelativeDataValidator : AbstractValidator<DriverRelativeData>
{
    public DriverRelativeDataValidator()
    {
        RuleFor(r => r.LastName).NotEmpty().MaximumLength(DriverRelative.MaxNameLength);
        RuleFor(r => r.FirstName).MaximumLength(DriverRelative.MaxNameLength);
        RuleFor(r => r.Relationship).MaximumLength(DriverRelative.MaxRelationshipLength);
        RuleFor(r => r.DateOfBirth)
            .Must(date => date!.Value.Year >= DriverRelative.MinYear)
            .When(r => r.DateOfBirth is not null)
            .WithMessage($"The relative's date of birth must not be before {DriverRelative.MinYear}.");
        RuleFor(r => r.Phone).MaximumLength(DriverRelative.MaxPhoneLength);
    }
}
