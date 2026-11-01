using FluentValidation;

namespace TransBrain.Application.Features.Customers.CustomerLocations.AddCustomerLocation;

/// <remarks>Length and format limits only; the name and address rules belong to the domain.</remarks>
public sealed class AddCustomerLocationCommandValidator : AbstractValidator<AddCustomerLocationCommand>
{
    public AddCustomerLocationCommandValidator()
    {
        RuleFor(c => c.Name).MaximumLength(200);
        RuleFor(c => c.Street).MaximumLength(200);
        RuleFor(c => c.PostalCode).MaximumLength(20);
        RuleFor(c => c.City).MaximumLength(200);
        RuleFor(c => c.Country).MaximumLength(2);
        RuleFor(c => c.ContactPerson).MaximumLength(200);
        RuleFor(c => c.Email).MaximumLength(320).EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.Phone).MaximumLength(50);
    }
}
