using FluentValidation;

namespace TransBrain.Application.Features.Invoices.ListInvoices;

public sealed class ListInvoicesQueryValidator : AbstractValidator<ListInvoicesQuery>
{
    public ListInvoicesQueryValidator()
    {
        // The same caps as the other list validators: without them every distinct page is a
        // distinct query and an authenticated caller can walk the number space freely.
        RuleFor(q => q.Page).InclusiveBetween(1, 10_000);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 100);
    }
}
