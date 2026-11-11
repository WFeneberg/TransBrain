using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Orders;

namespace TransBrain.Domain.Billing;

/// <summary>
/// A freight invoice for one customer: delivered transport orders billed at an agreed net price,
/// plus VAT. Frachtabrechnung, spec-wise the commercial close of the transport workflow.
/// </summary>
/// <remarks>
/// The customer name, billing address, VAT id and payment term are COPIES taken when the invoice
/// is created: an invoice is a document, and editing the customer later must not rewrite
/// invoices that were already written. The one rule this aggregate cannot enforce alone is
/// "a delivered order is billed at most once across invoices" — that spans all invoices, so the
/// CreateInvoice handler checks it against the repository. No unique index backs that check:
/// a cancelled invoice must release its orders for re-billing, and a partial index on the line
/// table cannot see the owning invoice's status, so the narrow race of two users billing the
/// same order in the same instant is accepted and undone by cancelling one of the invoices.
/// </remarks>
public sealed class Invoice
{
    private const decimal MaxVatRatePercent = 100m;

    private readonly List<InvoiceLine> _lines = [];

    // EF Core materialization only. Every other construction goes through Create.
    private Invoice()
    {
        InvoiceNumber = null!;
        CustomerName = null!;
        BillingAddress = null!;
    }

    private Invoice(
        Guid id,
        InvoiceNumber invoiceNumber,
        Guid customerId,
        string customerName,
        Address billingAddress,
        string? customerVatId,
        int paymentTermDays,
        decimal vatRatePercent,
        DateTimeOffset createdAt)
    {
        Id = id;
        InvoiceNumber = invoiceNumber;
        CustomerId = customerId;
        CustomerName = customerName;
        BillingAddress = billingAddress;
        CustomerVatId = customerVatId;
        PaymentTermDays = paymentTermDays;
        VatRatePercent = vatRatePercent;
        CreatedAt = createdAt;
        Status = InvoiceStatus.Draft;
    }

    public Guid Id { get; private set; }

    public InvoiceNumber InvoiceNumber { get; private set; }

    public Guid CustomerId { get; private set; }

    public string CustomerName { get; private set; }

    public Address BillingAddress { get; private set; }

    public string? CustomerVatId { get; private set; }

    /// <summary>Days from issuing until payment is due; copied from the customer at creation.</summary>
    public int PaymentTermDays { get; private set; }

    public decimal VatRatePercent { get; private set; }

    public InvoiceStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>When the invoice was issued; null while it is a draft.</summary>
    public DateTimeOffset? IssuedAt { get; private set; }

    /// <summary>Issue date plus the payment term; null while the invoice is a draft.</summary>
    public DateOnly? DueDate { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public decimal NetTotal => _lines.Sum(line => line.NetAmount);

    /// <summary>Kaufmännisch rounded to cents: away from zero, as German invoicing expects.</summary>
    public decimal VatAmount => Math.Round(NetTotal * VatRatePercent / 100m, 2, MidpointRounding.AwayFromZero);

    public decimal GrossTotal => NetTotal + VatAmount;

    public static Result<Invoice> Create(
        InvoiceNumber invoiceNumber,
        Customer customer,
        decimal vatRatePercent,
        DateTimeOffset createdAt)
    {
        if (vatRatePercent is < 0m or > MaxVatRatePercent)
        {
            return Error.Validation(
                "Invoice.VatRateOutOfRange",
                $"VAT rate must be between 0 and {MaxVatRatePercent} percent.");
        }

        return new Invoice(
            Guid.CreateVersion7(),
            invoiceNumber,
            customer.Id,
            customer.Name,
            customer.Address,
            customer.VatId,
            customer.PaymentTermDays,
            vatRatePercent,
            createdAt.ToUniversalTime());
    }

    public Result<Unit> AddLine(TransportOrder order, decimal netAmount, string? description = null)
    {
        if (Status != InvoiceStatus.Draft)
        {
            return NotEditable();
        }

        if (order.Status != OrderStatus.Delivered)
        {
            return Error.Conflict(
                "Invoice.OrderNotDelivered",
                $"Order '{order.OrderNumber.Value}' is '{order.Status}' and only delivered orders can be billed.");
        }

        if (_lines.Any(line => line.TransportOrderId == order.Id))
        {
            return Error.Conflict(
                "Invoice.OrderAlreadyOnInvoice",
                $"Order '{order.OrderNumber.Value}' is already on this invoice.");
        }

        if (netAmount <= 0m)
        {
            return Error.Validation(
                "Invoice.LineAmountNotPositive",
                "The net freight charge must be greater than zero.");
        }

        if (decimal.Round(netAmount, 2) != netAmount)
        {
            return Error.Validation(
                "Invoice.LineAmountTooPrecise",
                "The net freight charge must not have more than two decimal places.");
        }

        _lines.Add(InvoiceLine.Create(
            _lines.Count + 1,
            order.Id,
            order.OrderNumber.Value,
            OptionalText.Normalize(description) ?? $"Transport {order.OrderNumber.Value}",
            netAmount));

        return Unit.Value;
    }

    public Result<Unit> RemoveLine(Guid transportOrderId)
    {
        if (Status != InvoiceStatus.Draft)
        {
            return NotEditable();
        }

        if (_lines.All(line => line.TransportOrderId != transportOrderId))
        {
            return Error.NotFound(
                "Invoice.LineNotFound",
                $"No line for order '{transportOrderId}' on this invoice.");
        }

        _lines.RemoveAll(line => line.TransportOrderId == transportOrderId);
        Renumber();

        return Unit.Value;
    }

    public Result<Unit> Issue(DateTimeOffset issuedAt)
    {
        if (Status != InvoiceStatus.Draft)
        {
            return InvalidTransition(InvoiceStatus.Issued);
        }

        // An invoice without lines bills nothing; issuing it would create a numbered legal
        // document over zero euros.
        if (_lines.Count == 0)
        {
            return Error.Conflict("Invoice.NoLines", "An invoice without lines cannot be issued.");
        }

        Status = InvoiceStatus.Issued;
        IssuedAt = issuedAt.ToUniversalTime();
        DueDate = DateOnly.FromDateTime(IssuedAt.Value.UtcDateTime).AddDays(PaymentTermDays);

        return Unit.Value;
    }

    public Result<Unit> MarkPaid(DateTimeOffset paidAt)
    {
        if (Status != InvoiceStatus.Issued)
        {
            return InvalidTransition(InvoiceStatus.Paid);
        }

        Status = InvoiceStatus.Paid;
        PaidAt = paidAt.ToUniversalTime();

        return Unit.Value;
    }

    /// <remarks>
    /// Cancellable from Draft (the invoice was never sent) and from Issued (a Storno before
    /// payment). NOT from Paid: money already flowed, undoing that is a credit note — a new
    /// document, not a status flip on this one. Cancelling frees the billed orders, because
    /// "billed" is derived from lines of non-cancelled invoices rather than stored on the order.
    /// </remarks>
    public Result<Unit> Cancel()
    {
        if (Status is not (InvoiceStatus.Draft or InvoiceStatus.Issued))
        {
            return InvalidTransition(InvoiceStatus.Cancelled);
        }

        Status = InvoiceStatus.Cancelled;
        return Unit.Value;
    }

    /// <summary>The distinct order ids billed on this invoice, in line order.</summary>
    public IReadOnlyList<Guid> BilledOrderIds() =>
        _lines.OrderBy(line => line.Position).Select(line => line.TransportOrderId).ToList();

    // Positions stay contiguous from 1, same reasoning as Tour.Renumber: a gap breaks
    // "the next position is Count + 1" the moment anything relies on it.
    private void Renumber()
    {
        List<InvoiceLine> renumbered = _lines
            .OrderBy(line => line.Position)
            .Select((line, index) => line.WithPosition(index + 1))
            .ToList();

        _lines.Clear();
        _lines.AddRange(renumbered);
    }

    private Result<Unit> NotEditable() => Error.Conflict(
        "Invoice.NotEditable",
        $"An invoice in status '{Status}' no longer accepts changes to its lines.");

    private Result<Unit> InvalidTransition(InvoiceStatus to) => Error.Conflict(
        "Invoice.InvalidTransition",
        $"An invoice in status '{Status}' cannot move to '{to}'.");
}
