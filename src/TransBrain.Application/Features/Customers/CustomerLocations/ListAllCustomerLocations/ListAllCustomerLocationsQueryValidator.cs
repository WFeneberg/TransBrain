using FluentValidation;

namespace TransBrain.Application.Features.Customers.CustomerLocations.ListAllCustomerLocations;

public sealed class ListAllCustomerLocationsQueryValidator : AbstractValidator<ListAllCustomerLocationsQuery>
{
    public ListAllCustomerLocationsQueryValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, 10_000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
