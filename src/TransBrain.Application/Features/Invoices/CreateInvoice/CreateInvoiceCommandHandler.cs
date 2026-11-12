using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Invoices.CreateInvoice;

internal sealed class CreateInvoiceCommandHandler(
    IInvoiceRepository invoices,
    ICustomerRepository customers,
    ITransportOrderRepository orders,
    IInvoiceNumberGenerator invoiceNumbers,
    TimeProvider timeProvider)
    : ICommandHandler<CreateInvoiceCommand, InvoiceResponse>
{
    public async Task<Result<InvoiceResponse>> Handle(
        CreateInvoiceCommand command,
        CancellationToken cancellationToken)
    {
        Customer? customer = await customers.GetByIdAsync(command.CustomerId, cancellationToken);
        if (customer is null)
        {
            return Error.NotFound("Customer.NotFound", $"No customer with id '{command.CustomerId}'.");
        }

        // Load every order before touching anything, so a missing or unbillable one refuses the
        // whole command rather than leaving a half-built invoice.
        List<TransportOrder> billedOrders = [];
        foreach (InvoiceLinePayload line in command.Lines)
        {
            TransportOrder? order = await orders.GetByIdAsync(line.OrderId, cancellationToken);
            if (order is null)
            {
                return Error.NotFound("TransportOrder.NotFound", $"No order with id '{line.OrderId}'.");
            }

            billedOrders.Add(order);
        }

        IReadOnlyList<Guid> alreadyBilled = await invoices.GetBilledOrderIdsAsync(
            command.Lines.Select(line => line.OrderId).ToArray(), cancellationToken);

        if (alreadyBilled.Count > 0)
        {
            string numbers = string.Join(", ", billedOrders
                .Where(order => alreadyBilled.Contains(order.Id))
                .Select(order => order.OrderNumber.Value));

            return Error.Conflict(
                "Invoice.OrderAlreadyBilled",
                $"These orders are already billed on another invoice: {numbers}.");
        }

        // The number is drawn only after every refusal above has had its chance: a burnt
        // sequence number on a failed save is acceptable, one per rejected request is not.
        DateTimeOffset now = timeProvider.GetUtcNow();
        InvoiceNumber invoiceNumber = await invoiceNumbers.NextAsync(now.Year, cancellationToken);

        Result<Invoice> invoice = Invoice.Create(invoiceNumber, customer, command.VatRatePercent, now);
        if (!invoice.IsSuccess)
        {
            return invoice.Error!;
        }

        foreach (InvoiceLinePayload line in command.Lines)
        {
            TransportOrder order = billedOrders.Single(o => o.Id == line.OrderId);

            Result<Unit> added = invoice.Value.AddLine(order, line.NetAmount, line.Description);
            if (!added.IsSuccess)
            {
                return added.Error!;
            }
        }

        Result<Invoice> persisted = await invoices.AddAsync(invoice.Value, cancellationToken);
        if (!persisted.IsSuccess)
        {
            return persisted.Error!;
        }

        return InvoiceResponse.From(persisted.Value);
    }
}
