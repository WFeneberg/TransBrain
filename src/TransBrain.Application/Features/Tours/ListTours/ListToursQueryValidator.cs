using FluentValidation;

namespace TransBrain.Application.Features.Tours.ListTours;

public sealed class ListToursQueryValidator : AbstractValidator<ListToursQuery>
{
    public ListToursQueryValidator()
    {
        // The Page cap mirrors the other list validators: without it every distinct page is a
        // distinct query and an authenticated caller can walk the number space freely.
        RuleFor(q => q.Page).InclusiveBetween(1, 10_000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
