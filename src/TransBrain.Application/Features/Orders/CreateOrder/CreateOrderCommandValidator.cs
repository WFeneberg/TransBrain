using FluentValidation;

namespace TransBrain.Application.Features.Orders.CreateOrder;

/// <remarks>
/// These rules exist to report several field problems at once, which a domain factory returning
/// a single coded error cannot do. They deliberately mirror — and never extend — the domain's
/// own checks in Address, Cargo and TransportOrder: if the two ever disagree, the domain wins
/// and this is the copy to delete.
/// </remarks>
public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        // AddressPayload is a non-nullable record parameter, but nullable reference types are
        // not enforced at runtime: a JSON body omitting "consignor"/"consignee" deserialises to
        // null. RuleFor(c => c.Consignor.Name) on a null Consignor throws NullReferenceException
        // in FluentValidation rather than reporting a validation failure, so the nested property
        // rules are gated behind a When() null check.
        RuleFor(c => c.Consignor).NotNull();
        RuleFor(c => c.Consignee).NotNull();

        When(c => c.Consignor is not null, () =>
        {
            RuleFor(c => c.Consignor.Name).NotEmpty();
            RuleFor(c => c.Consignor.Street).NotEmpty();
            RuleFor(c => c.Consignor.PostalCode).NotEmpty();
            RuleFor(c => c.Consignor.City).NotEmpty();
        });

        When(c => c.Consignee is not null, () =>
        {
            RuleFor(c => c.Consignee.Name).NotEmpty();
            RuleFor(c => c.Consignee.Street).NotEmpty();
            RuleFor(c => c.Consignee.PostalCode).NotEmpty();
            RuleFor(c => c.Consignee.City).NotEmpty();
        });

        RuleFor(c => c.CargoDescription).NotEmpty();
        RuleFor(c => c.CargoWeightKg).GreaterThan(0);
        RuleFor(c => c.CargoLoadMeters).GreaterThan(0m);
    }
}
