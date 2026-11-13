using FluentValidation;

namespace TransBrain.Application.Features.Orders.OrderCharges.UpdateOrderCharge;

/// <remarks>The rules are shared with the add; see <see cref="OrderChargeWriteRules"/>.</remarks>
public sealed class UpdateOrderChargeCommandValidator : AbstractValidator<UpdateOrderChargeCommand>
{
    public UpdateOrderChargeCommandValidator()
    {
        Include(new OrderChargeWriteRules());
    }
}
