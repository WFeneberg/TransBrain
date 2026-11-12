using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Invoices.GetInvoiceById;

internal sealed class GetInvoiceByIdQueryHandler(IInvoiceRepository repository)
    : IQueryHandler<GetInvoiceByIdQuery, InvoiceResponse>
{
    public async Task<Result<InvoiceResponse>> Handle(
        GetInvoiceByIdQuery query,
        CancellationToken cancellationToken)
    {
        Invoice? invoice = await repository.GetByIdAsync(query.Id, cancellationToken);
        if (invoice is null)
        {
            return Error.NotFound("Invoice.NotFound", $"No invoice with id '{query.Id}'.");
        }

        return InvoiceResponse.From(invoice);
    }
}
