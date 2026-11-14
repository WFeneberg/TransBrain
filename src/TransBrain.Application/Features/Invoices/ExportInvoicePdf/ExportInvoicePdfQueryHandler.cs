using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Invoices.ExportInvoicePdf;

internal sealed class ExportInvoicePdfQueryHandler(
    IInvoiceRepository repository,
    IInvoiceDesignRepository designs,
    IInvoicePdfRenderer renderer)
    : IQueryHandler<ExportInvoicePdfQuery, InvoiceDocumentResponse>
{
    public async Task<Result<InvoiceDocumentResponse>> Handle(
        ExportInvoicePdfQuery query,
        CancellationToken cancellationToken)
    {
        Invoice? invoice = await repository.GetByIdAsync(query.Id, cancellationToken);
        if (invoice is null)
        {
            return Error.NotFound("Invoice.NotFound", $"No invoice with id '{query.Id}'.");
        }

        if (!invoice.IsDocument)
        {
            return NotADocument(invoice);
        }

        InvoiceDesign design = await designs.GetEffectiveAsync(cancellationToken);

        return new InvoiceDocumentResponse(
            $"{invoice.InvoiceNumber.Value}.pdf",
            "application/pdf",
            renderer.Render(invoice, design));
    }

    internal static Error NotADocument(Invoice invoice) => Error.Conflict(
        "Invoice.NotADocument",
        $"An invoice in status '{invoice.Status}' cannot be exported; only issued or paid invoices can.");
}
