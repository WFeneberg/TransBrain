using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;

namespace TransBrain.Application.Features.Invoices.ListInvoices;

public sealed record ListInvoicesQuery(
    int Page,
    int PageSize,
    string? Status = null,
    Guid? CustomerId = null) : IQuery<PagedResult<InvoiceResponse>>;
