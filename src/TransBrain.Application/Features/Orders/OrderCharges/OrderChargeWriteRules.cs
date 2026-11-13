using FluentValidation;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Orders.OrderCharges;

/// <remarks>Mirrors the checks of <see cref="OrderAdditionalCharge.Update"/>; if they disagree, the domain wins.</remarks>
internal sealed class OrderChargeWriteRules : AbstractValidator<IOrderChargeWriteCommand>
{
    public OrderChargeWriteRules()
    {
        RuleFor(c => c.Description).NotEmpty().MaximumLength(OrderAdditionalCharge.MaxDescriptionLength);
        RuleFor(c => c.Quantity)
            .GreaterThan(0m)
            .LessThanOrEqualTo(OrderAdditionalCharge.MaxQuantity)
            .PrecisionScale(12, 2, ignoreTrailingZeros: true)
            .WithMessage("The quantity must not have more than two decimal places.");
        RuleFor(c => c.Unit).MaximumLength(VehicleCatalogEntry.MaxCodeLength);
        RuleFor(c => c.UnitPrice)
            .InclusiveBetween(0m, OrderAdditionalCharge.MaxUnitPrice)
            .PrecisionScale(12, 2, ignoreTrailingZeros: true)
            .WithMessage("The unit price must not have more than two decimal places.");
    }
}
