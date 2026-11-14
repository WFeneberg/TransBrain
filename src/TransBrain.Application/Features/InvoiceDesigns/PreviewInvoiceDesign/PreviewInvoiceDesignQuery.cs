using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Invoices;

namespace TransBrain.Application.Features.InvoiceDesigns.PreviewInvoiceDesign;

/// <summary>
/// Renders a sample invoice as PDF with the given — possibly unsaved — design, so the designer
/// can show live what a real document would look like before anything is persisted.
/// </summary>
public sealed record PreviewInvoiceDesignQuery(InvoiceDesignPayload Design) : IQuery<InvoiceDocumentResponse>;
