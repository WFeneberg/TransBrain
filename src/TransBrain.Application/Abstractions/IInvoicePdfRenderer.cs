using TransBrain.Domain.Billing;

namespace TransBrain.Application.Abstractions;

/// <summary>
/// Renders an issued invoice as a printable PDF document, styled with the tenant's invoice
/// design (letterhead data, logo, colours, free texts).
/// </summary>
public interface IInvoicePdfRenderer
{
    byte[] Render(Invoice invoice, InvoiceDesign design);
}
