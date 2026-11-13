using FluentValidation;

namespace TransBrain.Application.Features.Orders.OrderTolls.UpdateOrderToll;

/// <remarks>The rules are shared with the add; see <see cref="OrderTollWriteRules"/>.</remarks>
public sealed class UpdateOrderTollCommandValidator : AbstractValidator<UpdateOrderTollCommand>
{
    public UpdateOrderTollCommandValidator()
    {
        Include(new OrderTollWriteRules());
    }
}
