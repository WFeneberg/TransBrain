using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Invoices.ExportInvoicePdf;

public sealed record ExportInvoicePdfQuery(Guid Id) : IQuery<InvoiceDocumentResponse>;
