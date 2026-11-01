using FluentValidation;

namespace TransBrain.Application.Features.Customers.ListCustomers;

public sealed class ListCustomersQueryValidator : AbstractValidator<ListCustomersQuery>
{
    // Same bound as the driver list: every distinct page mints a permanent cache entry, so an
    // unbounded page number would let any viewer inflate the cache index without limit.
    private const int MaxPage = 10_000;

    public ListCustomersQueryValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, MaxPage);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
