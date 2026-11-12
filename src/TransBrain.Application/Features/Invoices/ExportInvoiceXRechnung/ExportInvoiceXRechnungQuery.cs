using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Invoices.ExportInvoiceXRechnung;

public sealed record ExportInvoiceXRechnungQuery(Guid Id) : IQuery<InvoiceDocumentResponse>;
