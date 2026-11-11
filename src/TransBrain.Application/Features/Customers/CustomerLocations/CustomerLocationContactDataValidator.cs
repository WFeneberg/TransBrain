using FluentValidation;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers.CustomerLocations;

/// <remarks>Keyed "Contacts[i].&lt;Field&gt;" by the parent validators, so the form can mark the exact row.</remarks>
internal sealed class CustomerLocationContactDataValidator : AbstractValidator<CustomerLocationContactData>
{
    public CustomerLocationContactDataValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(CustomerLocationContact.MaxNameLength);
        RuleFor(c => c.Phone).MaximumLength(CustomerLocationContact.MaxPhoneLength);
        RuleFor(c => c.Email)
            .MaximumLength(CustomerLocationContact.MaxEmailLength)
            .EmailAddress()
            .When(c => !string.IsNullOrWhiteSpace(c.Email));
    }
}
