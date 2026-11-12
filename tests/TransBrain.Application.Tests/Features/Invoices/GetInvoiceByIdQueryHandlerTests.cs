using AwesomeAssertions;
using TransBrain.Application.Features.Invoices;
using TransBrain.Application.Features.Invoices.GetInvoiceById;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Invoices;

public class GetInvoiceByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingInvoice_ReturnsResponseWithLines()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        Invoice invoice = fixture.DraftInvoice(netAmount: 250m);
        GetInvoiceByIdQueryHandler handler = new(fixture.Invoices);

        Result<InvoiceResponse> result = await handler.Handle(
            new GetInvoiceByIdQuery(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(invoice.Id);
        result.Value.InvoiceNumber.Should().Be(invoice.InvoiceNumber.Value);
        result.Value.Lines.Should().ContainSingle();
        result.Value.NetTotal.Should().Be(250m);
        result.Value.VatAmount.Should().Be(47.50m);
        result.Value.GrossTotal.Should().Be(297.50m);
        result.Value.BillingAddress.City.Should().Be("München");
    }

    [Fact]
    public async Task Handle_UnknownInvoice_ReturnsNotFound()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        GetInvoiceByIdQueryHandler handler = new(fixture.Invoices);

        Result<InvoiceResponse> result = await handler.Handle(
            new GetInvoiceByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Invoice.NotFound");
    }
}
