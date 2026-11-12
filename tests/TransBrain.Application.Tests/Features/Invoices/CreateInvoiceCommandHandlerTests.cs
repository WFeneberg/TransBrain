using AwesomeAssertions;
using TransBrain.Application.Features.Invoices;
using TransBrain.Application.Features.Invoices.CreateInvoice;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Tests.Features.Invoices;

public class CreateInvoiceCommandHandlerTests
{
    private static CreateInvoiceCommandHandler Handler(InvoiceFixture fixture, int firstSequence = 1)
        => new(
            fixture.Invoices,
            fixture.Customers,
            fixture.Orders,
            new StubInvoiceNumberGenerator(firstSequence),
            fixture.Clock);

    [Fact]
    public async Task Handle_DeliveredOrders_PersistsDraftInvoiceWithLinesAndTotals()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        TransportOrder first = fixture.DeliveredOrder(1);
        TransportOrder second = fixture.DeliveredOrder(2);

        Result<InvoiceResponse> result = await Handler(fixture).Handle(
            new CreateInvoiceCommand(fixture.Customer.Id, 19m, [
                new InvoiceLinePayload(first.Id, 450.50m),
                new InvoiceLinePayload(second.Id, 120m, "Expresszuschlag inklusive")
            ]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.InvoiceNumber.Should().Be("RE-2027-00001");
        result.Value.Status.Should().Be("Draft");
        result.Value.CustomerName.Should().Be("Spedition Kunde GmbH");
        result.Value.Lines.Should().HaveCount(2);
        result.Value.Lines[0].OrderNumber.Should().Be(first.OrderNumber.Value);
        result.Value.Lines[1].Description.Should().Be("Expresszuschlag inklusive");
        result.Value.NetTotal.Should().Be(570.50m);
        result.Value.VatAmount.Should().Be(108.40m);
        result.Value.GrossTotal.Should().Be(678.90m);
        fixture.Invoices.Invoices.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_UnknownCustomer_ReturnsNotFoundAndPersistsNothing()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        TransportOrder order = fixture.DeliveredOrder();

        Result<InvoiceResponse> result = await Handler(fixture).Handle(
            new CreateInvoiceCommand(Guid.NewGuid(), 19m, [new InvoiceLinePayload(order.Id, 100m)]),
            CancellationToken.None);

        result.Error!.Code.Should().Be("Customer.NotFound");
        fixture.Invoices.Invoices.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnknownOrder_ReturnsNotFound()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();

        Result<InvoiceResponse> result = await Handler(fixture).Handle(
            new CreateInvoiceCommand(fixture.Customer.Id, 19m, [new InvoiceLinePayload(Guid.NewGuid(), 100m)]),
            CancellationToken.None);

        result.Error!.Code.Should().Be("TransportOrder.NotFound");
        fixture.Invoices.Invoices.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_OrderOnAnotherInvoice_ReturnsConflictNamingTheOrder()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        fixture.DraftInvoice(invoiceSequence: 1, orderSequence: 1);
        TransportOrder billed = fixture.Orders.Orders.Single();

        Result<InvoiceResponse> result = await Handler(fixture, firstSequence: 2).Handle(
            new CreateInvoiceCommand(fixture.Customer.Id, 19m, [new InvoiceLinePayload(billed.Id, 99m)]),
            CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.OrderAlreadyBilled");
        result.Error.Message.Should().Contain(billed.OrderNumber.Value);
        fixture.Invoices.Invoices.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_OrderOnACancelledInvoice_BillsItAgain()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        fixture.DraftInvoice(invoiceSequence: 1, orderSequence: 1).Cancel();
        TransportOrder freed = fixture.Orders.Orders.Single();

        Result<InvoiceResponse> result = await Handler(fixture, firstSequence: 2).Handle(
            new CreateInvoiceCommand(fixture.Customer.Id, 19m, [new InvoiceLinePayload(freed.Id, 99m)]),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        fixture.Invoices.Invoices.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_UndeliveredOrder_ReturnsDomainConflictAndPersistsNothing()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        TransportOrder order = InvoiceFixture.AnOrder(delivered: false);
        fixture.Orders.Seed(order);

        Result<InvoiceResponse> result = await Handler(fixture).Handle(
            new CreateInvoiceCommand(fixture.Customer.Id, 19m, [new InvoiceLinePayload(order.Id, 100m)]),
            CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.OrderNotDelivered");
        fixture.Invoices.Invoices.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_VatRateOutOfRange_ReturnsDomainValidationError()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        TransportOrder order = fixture.DeliveredOrder();

        Result<InvoiceResponse> result = await Handler(fixture).Handle(
            new CreateInvoiceCommand(fixture.Customer.Id, 101m, [new InvoiceLinePayload(order.Id, 100m)]),
            CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.VatRateOutOfRange");
    }

    [Fact]
    public async Task Handle_TwoInvoices_TakesTheNumberFromTheGeneratorInSequence()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        CreateInvoiceCommandHandler handler = Handler(fixture);

        Result<InvoiceResponse> first = await handler.Handle(
            new CreateInvoiceCommand(
                fixture.Customer.Id, 19m, [new InvoiceLinePayload(fixture.DeliveredOrder(1).Id, 100m)]),
            CancellationToken.None);
        Result<InvoiceResponse> second = await handler.Handle(
            new CreateInvoiceCommand(
                fixture.Customer.Id, 19m, [new InvoiceLinePayload(fixture.DeliveredOrder(2).Id, 100m)]),
            CancellationToken.None);

        first.Value.InvoiceNumber.Should().EndWith("-00001");
        second.Value.InvoiceNumber.Should().EndWith("-00002");
    }
}
