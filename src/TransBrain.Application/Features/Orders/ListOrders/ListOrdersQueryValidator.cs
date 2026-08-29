using FluentValidation;

namespace TransBrain.Application.Features.Orders.ListOrders;

public sealed class ListOrdersQueryValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersQueryValidator()
    {
        // The Page cap mirrors the two master-data validators: without it every distinct page is
        // a distinct query and an authenticated caller can walk the number space freely.
        RuleFor(q => q.Page).InclusiveBetween(1, 10_000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);

        // A request-shape rule with no domain equivalent: TimeWindow validates an order's own
        // window, not a search range, so this belongs here rather than in the domain.
        RuleFor(q => q.PickupTo)
            .GreaterThanOrEqualTo(q => q.PickupFrom!.Value)
            .When(q => q.PickupFrom is not null && q.PickupTo is not null)
            .WithMessage("'Pickup To' must not be earlier than 'Pickup From'.");
    }
}
