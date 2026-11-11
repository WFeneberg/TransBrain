using FluentValidation;
using TransBrain.Domain.Customers;

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
        RuleFor(c => c.Contacts!.Count)
            .LessThanOrEqualTo(CustomerLocation.MaxContacts)
            .When(c => c.Contacts is not null)
            .OverridePropertyName(nameof(AddCustomerLocationCommand.Contacts))
            .WithMessage($"A delivery location can have at most {CustomerLocation.MaxContacts} contact persons.");
        RuleForEach(c => c.Contacts).SetValidator(new CustomerLocationContactDataValidator());
    }
}
