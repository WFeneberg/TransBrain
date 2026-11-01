using FluentValidation;

namespace TransBrain.Application.Features.Customers.UpdateCustomer;

/// <remarks>Mirrors CreateCustomerCommandValidator: same column limits, same e-mail rule.</remarks>
public sealed class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(c => c.Name).MaximumLength(200);
        RuleFor(c => c.Street).MaximumLength(200);
        RuleFor(c => c.PostalCode).MaximumLength(20);
        RuleFor(c => c.City).MaximumLength(200);
        RuleFor(c => c.Country).MaximumLength(2);
        RuleFor(c => c.ContactPerson).MaximumLength(200);
        RuleFor(c => c.Email).MaximumLength(320).EmailAddress().When(c => !string.IsNullOrWhiteSpace(c.Email));
        RuleFor(c => c.Phone).MaximumLength(50);
        RuleFor(c => c.VatId).MaximumLength(20);
    }
}
