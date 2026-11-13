using FluentValidation;

namespace TransBrain.Application.Features.Orders.OrderCharges.AddOrderCharge;

/// <remarks>The rules are shared with the update; see <see cref="OrderChargeWriteRules"/>.</remarks>
public sealed class AddOrderChargeCommandValidator : AbstractValidator<AddOrderChargeCommand>
{
    public AddOrderChargeCommandValidator()
    {
        Include(new OrderChargeWriteRules());
    }
}
