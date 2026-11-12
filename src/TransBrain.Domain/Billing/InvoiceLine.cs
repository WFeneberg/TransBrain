namespace TransBrain.Domain.Billing;

/// <summary>
/// One billed transport order on an invoice: the net freight charge agreed for that order.
/// </summary>
/// <remarks>
/// Created only by <see cref="Invoice"/>, which is what keeps the one-line-per-order and
/// contiguous-position invariants true: nothing outside the aggregate can add a line. The order
/// number and description are copies — an invoice is a document, and what it says must not
/// change because the order it was built from changes later.
/// </remarks>
public sealed record InvoiceLine
{
    private InvoiceLine(int position, Guid transportOrderId, string orderNumber, string description, decimal netAmount)
    {
        Position = position;
        TransportOrderId = transportOrderId;
        OrderNumber = orderNumber;
        Description = description;
        NetAmount = netAmount;
    }

    public int Position { get; }

    public Guid TransportOrderId { get; }

    public string OrderNumber { get; }

    public string Description { get; }

    /// <summary>The net freight charge for this order, in euros.</summary>
    public decimal NetAmount { get; }

    internal static InvoiceLine Create(
        int position,
        Guid transportOrderId,
        string orderNumber,
        string description,
        decimal netAmount) =>
        new(position, transportOrderId, orderNumber, description, netAmount);

    internal InvoiceLine WithPosition(int position) =>
        new(position, TransportOrderId, OrderNumber, Description, NetAmount);
}
