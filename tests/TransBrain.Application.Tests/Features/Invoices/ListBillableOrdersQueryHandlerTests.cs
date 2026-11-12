using AwesomeAssertions;
using TransBrain.Application.Features.Invoices.ListBillableOrders;
using TransBrain.Application.Features.Orders;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Features.Invoices;

public class ListBillableOrdersQueryHandlerTests
{
    [Fact]
    public async Task Handle_MixedOrders_ReturnsOnlyDeliveredUnbilledOnes()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        fixture.DraftInvoice(invoiceSequence: 1, orderSequence: 1);
        TransportOrder unbilled = fixture.DeliveredOrder(2);
        TransportOrder undelivered = InvoiceFixture.AnOrder(3, delivered: false);
        fixture.Invoices.SeedOrders(undelivered);
        ListBillableOrdersQueryHandler handler = new(fixture.Invoices);

        Result<IReadOnlyList<OrderResponse>> result = await handler.Handle(
            new ListBillableOrdersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].Id.Should().Be(unbilled.Id);
        result.Value[0].Status.Should().Be("Delivered");
    }

    [Fact]
    public async Task Handle_BilledInvoiceCancelled_OrderBecomesBillableAgain()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        fixture.DraftInvoice(invoiceSequence: 1, orderSequence: 1).Cancel();
        ListBillableOrdersQueryHandler handler = new(fixture.Invoices);

        Result<IReadOnlyList<OrderResponse>> result = await handler.Handle(
            new ListBillableOrdersQuery(), CancellationToken.None);

        result.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_NothingDelivered_ReturnsEmptyList()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        ListBillableOrdersQueryHandler handler = new(fixture.Invoices);

        Result<IReadOnlyList<OrderResponse>> result = await handler.Handle(
            new ListBillableOrdersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
