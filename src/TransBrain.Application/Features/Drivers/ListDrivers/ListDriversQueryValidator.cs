using FluentValidation;

namespace TransBrain.Application.Features.Drivers.ListDrivers;

public sealed class ListDriversQueryValidator : AbstractValidator<ListDriversQuery>
{
    public ListDriversQueryValidator()
    {
        RuleFor(q => q.Page).GreaterThan(0);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
