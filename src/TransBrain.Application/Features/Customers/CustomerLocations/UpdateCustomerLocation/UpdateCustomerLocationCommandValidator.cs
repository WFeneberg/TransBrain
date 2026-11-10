using FluentValidation;
using TransBrain.Domain.Customers;

namespace TransBrain.Application.Features.Customers.CustomerLocations.UpdateCustomerLocation;

/// <remarks>Mirrors AddCustomerLocationCommandValidator.</remarks>
public sealed class UpdateCustomerLocationCommandValidator : AbstractValidator<UpdateCustomerLocationCommand>
{
    public UpdateCustomerLocationCommandValidator()
    {
        RuleFor(c => c.Name).MaximumLength(200);
        RuleFor(c => c.Street).MaximumLength(200);
        RuleFor(c => c.PostalCode).MaximumLength(20);
        RuleFor(c => c.City).MaximumLength(200);
        RuleFor(c => c.Country).MaximumLength(2);
        RuleFor(c => c.Contacts!.Count)
            .LessThanOrEqualTo(CustomerLocation.MaxContacts)
            .When(c => c.Contacts is not null)
            .OverridePropertyName(nameof(UpdateCustomerLocationCommand.Contacts))
            .WithMessage($"A delivery location can have at most {CustomerLocation.MaxContacts} contact persons.");
        RuleForEach(c => c.Contacts).SetValidator(new CustomerLocationContactDataValidator());
    }
}
