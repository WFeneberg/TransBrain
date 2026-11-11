using FluentValidation;

namespace TransBrain.Application.Features.Geodata.ResolveGeodata;

/// <remarks>Length limits only; the address rules belong to the domain.</remarks>
public sealed class ResolveGeodataCommandValidator : AbstractValidator<ResolveGeodataCommand>
{
    public ResolveGeodataCommandValidator()
    {
        RuleFor(c => c.Street).MaximumLength(200);
        RuleFor(c => c.PostalCode).MaximumLength(20);
        RuleFor(c => c.City).MaximumLength(200);
        RuleFor(c => c.Country).MaximumLength(2);
    }
}
