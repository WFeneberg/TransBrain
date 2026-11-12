using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Invoices.ExportInvoicePdf;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Invoices.ExportInvoiceXRechnung;

internal sealed class ExportInvoiceXRechnungQueryHandler(
    IInvoiceRepository repository,
    IXRechnungWriter writer)
    : IQueryHandler<ExportInvoiceXRechnungQuery, InvoiceDocumentResponse>
{
    public async Task<Result<InvoiceDocumentResponse>> Handle(
        ExportInvoiceXRechnungQuery query,
        CancellationToken cancellationToken)
    {
        Invoice? invoice = await repository.GetByIdAsync(query.Id, cancellationToken);
        if (invoice is null)
        {
            return Error.NotFound("Invoice.NotFound", $"No invoice with id '{query.Id}'.");
        }

        if (!invoice.IsDocument)
        {
            return ExportInvoicePdfQueryHandler.NotADocument(invoice);
        }

        return new InvoiceDocumentResponse(
            $"{invoice.InvoiceNumber.Value}-xrechnung.xml",
            "application/xml",
            writer.Write(invoice));
    }
}
