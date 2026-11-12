using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Invoices.ListInvoices;

/// <remarks>
/// Deliberately not cached, like the order list: invoices change status too often for the
/// invalidation cost, and billing screens must never show a stale status.
/// </remarks>
internal sealed class ListInvoicesQueryHandler(IInvoiceRepository repository)
    : IQueryHandler<ListInvoicesQuery, PagedResult<InvoiceResponse>>
{
    public async Task<Result<PagedResult<InvoiceResponse>>> Handle(
        ListInvoicesQuery query,
        CancellationToken cancellationToken)
    {
        InvoiceStatus? status = null;

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            // Enum.TryParse accepts numeric strings, so "99" would otherwise parse into an
            // undefined member and silently filter on it. IsDefined closes that gap.
            if (!Enum.TryParse(query.Status, ignoreCase: true, out InvoiceStatus parsed)
                || !Enum.IsDefined(parsed))
            {
                return Error.Validation(
                    "Invoice.UnknownStatus",
                    $"'{query.Status}' is not a known invoice status.");
            }

            status = parsed;
        }

        int skip = (query.Page - 1) * query.PageSize;

        IReadOnlyList<Invoice> invoices = await repository.ListAsync(
            skip, query.PageSize, status, query.CustomerId, cancellationToken);

        int totalCount = await repository.CountAsync(status, query.CustomerId, cancellationToken);

        InvoiceResponse[] items = invoices.Select(InvoiceResponse.From).ToArray();

        return new PagedResult<InvoiceResponse>(items, query.Page, query.PageSize, totalCount);
    }
}
