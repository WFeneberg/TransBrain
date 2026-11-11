using AwesomeAssertions;
using TransBrain.Application.Features.Invoices;
using TransBrain.Application.Features.Invoices.MarkInvoicePaid;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Invoices;

public class MarkInvoicePaidCommandHandlerTests
{
    [Fact]
    public async Task Handle_IssuedInvoice_MarksPaidWithTimestamp()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        Invoice invoice = fixture.DraftInvoice();
        invoice.Issue(InvoiceFixture.Now.AddDays(-7));
        MarkInvoicePaidCommandHandler handler = new(fixture.Invoices, fixture.Clock);

        Result<InvoiceResponse> result = await handler.Handle(
            new MarkInvoicePaidCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Paid");
        result.Value.PaidAt.Should().Be(InvoiceFixture.Now);
        fixture.Invoices.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownInvoice_ReturnsNotFound()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        MarkInvoicePaidCommandHandler handler = new(fixture.Invoices, fixture.Clock);

        Result<InvoiceResponse> result = await handler.Handle(
            new MarkInvoicePaidCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.NotFound");
    }

    [Fact]
    public async Task Handle_DraftInvoice_ReturnsConflictAndSavesNothing()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        Invoice invoice = fixture.DraftInvoice();
        MarkInvoicePaidCommandHandler handler = new(fixture.Invoices, fixture.Clock);

        Result<InvoiceResponse> result = await handler.Handle(
            new MarkInvoicePaidCommand(invoice.Id), CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.InvalidTransition");
        fixture.Invoices.SaveChangesCallCount.Should().Be(0);
    }
}
