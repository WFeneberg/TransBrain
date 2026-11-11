using FluentValidation;

namespace TransBrain.Application.Features.Geodata.SetGeodataOverride;

/// <remarks>Length and range limits only; the address and coordinate rules belong to the domain.</remarks>
public sealed class SetGeodataOverrideCommandValidator : AbstractValidator<SetGeodataOverrideCommand>
{
    public SetGeodataOverrideCommandValidator()
    {
        RuleFor(c => c.Street).MaximumLength(200);
        RuleFor(c => c.PostalCode).MaximumLength(20);
        RuleFor(c => c.City).MaximumLength(200);
        RuleFor(c => c.Country).MaximumLength(2);
        RuleFor(c => c.Latitude).InclusiveBetween(-90, 90);
        RuleFor(c => c.Longitude).InclusiveBetween(-180, 180);
    }
}
