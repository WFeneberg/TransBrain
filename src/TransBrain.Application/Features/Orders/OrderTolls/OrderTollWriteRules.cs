using FluentValidation;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Orders.OrderTolls;

/// <remarks>Mirrors the checks of <see cref="OrderTollCharge.Update"/>; if they disagree, the domain wins.</remarks>
internal sealed class OrderTollWriteRules : AbstractValidator<IOrderTollWriteCommand>
{
    public OrderTollWriteRules()
    {
        RuleFor(c => c.TollCategory).NotEmpty().MaximumLength(VehicleCatalogEntry.MaxCodeLength);
        RuleFor(c => c.EmptyKm).InclusiveBetween(0, OrderTollCharge.MaxKilometers);
        RuleFor(c => c.LoadedKm).InclusiveBetween(0, OrderTollCharge.MaxKilometers);
        RuleFor(c => c.FactorPerKm)
            .InclusiveBetween(0m, OrderTollCharge.MaxFactorPerKm)
            .PrecisionScale(8, 4, ignoreTrailingZeros: true)
            .WithMessage("The factor per kilometre must not have more than four decimal places.");
    }
}
