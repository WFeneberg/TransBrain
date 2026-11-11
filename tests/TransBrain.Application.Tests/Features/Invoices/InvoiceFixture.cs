using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Features.Invoices;

/// <summary>
/// A customer and delivered orders already seeded into the fakes every invoice handler needs.
/// Shared across the invoice handler test classes for the same reason as TourFixture: building
/// this inline each time would bury each test's actual arrangement under scaffolding.
/// </summary>
internal sealed record InvoiceFixture(
    InMemoryInvoiceRepository Invoices,
    InMemoryCustomerRepository Customers,
    InMemoryTransportOrderRepository Orders,
    Customer Customer)
{
    public static readonly DateTimeOffset Now = new(2027, 3, 10, 9, 0, 0, TimeSpan.Zero);

    public static InvoiceFixture Create(int paymentTermDays = 30)
    {
        InMemoryInvoiceRepository invoices = new();
        InMemoryCustomerRepository customers = new();
        InMemoryTransportOrderRepository orders = new();

        Customer customer = ACustomer(paymentTermDays);
        customers.Seed(customer);

        return new InvoiceFixture(invoices, customers, orders, customer);
    }

    public static Customer ACustomer(int paymentTermDays = 30) =>
        Customer.Create(
            "Spedition Kunde GmbH",
            Address.Create("Spedition Kunde GmbH", "Hauptstr. 1", "80331", "München", "DE").Value,
            null,
            null,
            null,
            "DE123456789",
            paymentTermDays).Value;

    public static TransportOrder AnOrder(int sequence = 1, bool delivered = true)
    {
        DateTimeOffset pickup = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);
        Address consignor = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;
        Address consignee = Address.Create("Empfaenger AG", "Bahnhofstr. 2", "10115", "Berlin", "DE").Value;

        TransportOrder order = TransportOrder.Create(
            OrderNumber.From(2027, sequence),
            consignor,
            consignee,
            Cargo.Create("Palettenware", 5_000, 4.0m).Value,
            TimeWindow.Create(pickup, pickup.AddHours(2)).Value,
            TimeWindow.Create(pickup.AddHours(4), pickup.AddHours(8)).Value,
            pickup.AddDays(-30)).Value;

        if (delivered)
        {
            order.MarkPlanned();
            order.MarkInTransit();
            order.MarkDelivered();
        }

        return order;
    }

    /// <summary>Seeds a delivered order into both order pools the invoice fakes read from.</summary>
    public TransportOrder DeliveredOrder(int sequence = 1)
    {
        TransportOrder order = AnOrder(sequence);
        Orders.Seed(order);
        Invoices.SeedOrders(order);
        return order;
    }

    /// <summary>A draft invoice over one delivered order, seeded into the invoice fake.</summary>
    public Invoice DraftInvoice(int invoiceSequence = 1, int orderSequence = 1, decimal netAmount = 100m)
    {
        Invoice invoice = Invoice.Create(
            InvoiceNumber.From(2027, invoiceSequence), Customer, 19m, Now).Value;
        invoice.AddLine(DeliveredOrder(orderSequence), netAmount);
        Invoices.Seed(invoice);
        return invoice;
    }

    public FixedTimeProvider Clock { get; } = new(Now);
}
