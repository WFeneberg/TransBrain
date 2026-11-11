using TransBrain.Domain.Billing;

namespace TransBrain.Application.Abstractions;

/// <summary>Renders an issued invoice as a printable PDF document.</summary>
public interface IInvoicePdfRenderer
{
    byte[] Render(Invoice invoice);
}
