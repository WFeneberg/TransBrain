using TransBrain.Application.Features.Orders;
using TransBrain.Domain.Billing;

namespace TransBrain.Application.Features.Invoices;

public sealed record InvoiceLineResponse(
    int Position,
    Guid TransportOrderId,
    string OrderNumber,
    string Description,
    decimal NetAmount)
{
    public static InvoiceLineResponse From(InvoiceLine line) => new(
        line.Position,
        line.TransportOrderId,
        line.OrderNumber,
        line.Description,
        line.NetAmount);
}

public sealed record InvoiceResponse(
    Guid Id,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    AddressPayload BillingAddress,
    string? CustomerVatId,
    Guid? BillToLocationId,
    int PaymentTermDays,
    decimal VatRatePercent,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? IssuedAt,
    DateOnly? DueDate,
    DateTimeOffset? PaidAt,
    decimal NetTotal,
    decimal VatAmount,
    decimal GrossTotal,
    IReadOnlyList<InvoiceLineResponse> Lines)
{
    public static InvoiceResponse From(Invoice invoice) => new(
        invoice.Id,
        invoice.InvoiceNumber.Value,
        invoice.CustomerId,
        invoice.CustomerName,
        AddressPayload.From(invoice.BillingAddress),
        invoice.CustomerVatId,
        invoice.BillToLocationId,
        invoice.PaymentTermDays,
        invoice.VatRatePercent,
        invoice.Status.ToString(),
        invoice.CreatedAt,
        invoice.IssuedAt,
        invoice.DueDate,
        invoice.PaidAt,
        invoice.NetTotal,
        invoice.VatAmount,
        invoice.GrossTotal,
        invoice.Lines.Select(InvoiceLineResponse.From).ToArray());
}
