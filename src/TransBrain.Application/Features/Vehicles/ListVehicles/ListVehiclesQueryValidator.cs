using FluentValidation;

namespace TransBrain.Application.Features.Vehicles.ListVehicles;

public sealed class ListVehiclesQueryValidator : AbstractValidator<ListVehiclesQuery>
{
    // Every distinct Page mints a permanent cache entry and index member (see
    // RedisCacheService), and RemoveByPrefixAsync does one Redis round-trip per indexed member
    // on every write - so an unbounded Page lets any authenticated viewer inflate both without
    // limit. 10,000 is generous headroom (at the max PageSize of 100, that is 1,000,000 rows -
    // an order of magnitude beyond any realistic fleet size for a single haulier) while still
    // bounding the cache and its index to a fixed, small multiple of that per filter combination.
    private const int MaxPage = 10_000;

    public ListVehiclesQueryValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, MaxPage);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
