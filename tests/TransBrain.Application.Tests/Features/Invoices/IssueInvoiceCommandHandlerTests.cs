using AwesomeAssertions;
using TransBrain.Application.Features.Invoices;
using TransBrain.Application.Features.Invoices.IssueInvoice;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Invoices;

public class IssueInvoiceCommandHandlerTests
{
    [Fact]
    public async Task Handle_DraftInvoice_IssuesAndComputesDueDateFromPaymentTerm()
    {
        InvoiceFixture fixture = InvoiceFixture.Create(paymentTermDays: 14);
        Invoice invoice = fixture.DraftInvoice();
        IssueInvoiceCommandHandler handler = new(fixture.Invoices, fixture.Clock);

        Result<InvoiceResponse> result = await handler.Handle(
            new IssueInvoiceCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Issued");
        result.Value.IssuedAt.Should().Be(InvoiceFixture.Now);
        result.Value.DueDate.Should().Be(new DateOnly(2027, 3, 24));
        fixture.Invoices.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownInvoice_ReturnsNotFound()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        IssueInvoiceCommandHandler handler = new(fixture.Invoices, fixture.Clock);

        Result<InvoiceResponse> result = await handler.Handle(
            new IssueInvoiceCommand(Guid.NewGuid()), CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.NotFound");
    }

    [Fact]
    public async Task Handle_AlreadyIssued_ReturnsConflictAndSavesNothing()
    {
        InvoiceFixture fixture = InvoiceFixture.Create();
        Invoice invoice = fixture.DraftInvoice();
        invoice.Issue(InvoiceFixture.Now);
        IssueInvoiceCommandHandler handler = new(fixture.Invoices, fixture.Clock);

        Result<InvoiceResponse> result = await handler.Handle(
            new IssueInvoiceCommand(invoice.Id), CancellationToken.None);

        result.Error!.Code.Should().Be("Invoice.InvalidTransition");
        fixture.Invoices.SaveChangesCallCount.Should().Be(0);
    }
}
