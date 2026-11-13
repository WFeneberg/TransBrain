using FluentValidation;

namespace TransBrain.Application.Features.Orders.OrderTolls.AddOrderToll;

/// <remarks>The rules are shared with the update; see <see cref="OrderTollWriteRules"/>.</remarks>
public sealed class AddOrderTollCommandValidator : AbstractValidator<AddOrderTollCommand>
{
    public AddOrderTollCommandValidator()
    {
        Include(new OrderTollWriteRules());
    }
}
