using FluentValidation;

namespace TransBrain.Application.Features.Customers.CreateCustomer;

/// <remarks>
/// Length and format limits here protect the database columns and reject malformed e-mail
/// addresses at the trust boundary. Name, address and payment-term rules are enforced by the
/// domain, so they are not repeated here.
/// </remarks>
public sealed class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
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
        RuleFor(c => c.Additional!).SetValidator(new CustomerAdditionalDataValidator()).When(c => c.Additional is not null);
    }
}
