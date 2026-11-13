using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Invoices.CreateInvoice;

public sealed record InvoiceLinePayload(Guid OrderId, decimal NetAmount, string? Description = null);

/// <remarks>
/// <see cref="BillToLocationId"/> addresses the invoice to a delivery location with its own
/// company name instead of the customer; null bills the customer itself.
/// </remarks>
public sealed record CreateInvoiceCommand(
    Guid CustomerId,
    decimal VatRatePercent,
    IReadOnlyList<InvoiceLinePayload> Lines,
    Guid? BillToLocationId = null) : ICommand<InvoiceResponse>;
