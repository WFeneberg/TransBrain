using FluentValidation;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Customers;

/// <remarks>
/// Mirrors <see cref="CustomerAdditionalInfo.Create"/> so all field problems are reported at once,
/// keyed "Additional.&lt;Field&gt;" by the parent validators.
/// </remarks>
internal sealed class CustomerAdditionalDataValidator : AbstractValidator<CustomerAdditionalData>
{
    public CustomerAdditionalDataValidator()
    {
        RuleFor(a => a.SecondaryName).MaximumLength(CustomerAdditionalInfo.MaxNameLength);
        RuleFor(a => a.SecondaryStreet).MaximumLength(CustomerAdditionalInfo.MaxStreetLength);
        RuleFor(a => a.SecondaryPostalCode).MaximumLength(CustomerAdditionalInfo.MaxPostalCodeLength);
        RuleFor(a => a.SecondaryCity).MaximumLength(CustomerAdditionalInfo.MaxCityLength);
        RuleFor(a => a.SecondaryCountry)
            .Must(country => country!.Trim().Length == CustomerAdditionalInfo.CountryLength
                && country.Trim().All(char.IsAsciiLetter))
            .When(a => !string.IsNullOrWhiteSpace(a.SecondaryCountry))
            .WithMessage("Country must be an ISO 3166-1 alpha-2 code, for example 'DE'.");
        RuleFor(a => a.FoundedOn)
            .Must(date => date!.Value.Year >= CustomerAdditionalInfo.MinFoundingYear)
            .When(a => a.FoundedOn is not null)
            .WithMessage($"The founding date must not be before {CustomerAdditionalInfo.MinFoundingYear}.");
        RuleFor(a => a.WorkerCount).InclusiveBetween(0, CustomerAdditionalInfo.MaxHeadcount);
        RuleFor(a => a.SalariedEmployeeCount).InclusiveBetween(0, CustomerAdditionalInfo.MaxHeadcount);
        RuleFor(a => a.CustomerClass).MaximumLength(VehicleCatalogEntry.MaxCodeLength);
        RuleFor(a => a.Comment).MaximumLength(CustomerAdditionalInfo.MaxCommentLength);
    }
}
