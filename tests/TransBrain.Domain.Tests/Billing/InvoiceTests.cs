using AwesomeAssertions;
using TransBrain.Domain.Billing;
using TransBrain.Domain.Common;
using TransBrain.Domain.Customers;
using TransBrain.Domain.Orders;

namespace TransBrain.Domain.Tests.Billing;

public class InvoiceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2027, 3, 10, 9, 0, 0, TimeSpan.Zero);

    private static Customer ACustomer(int paymentTermDays = 30) =>
        Customer.Create(
            "Spedition Kunde GmbH",
            Address.Create("Spedition Kunde GmbH", "Hauptstr. 1", "80331", "München", "DE").Value,
            null,
            null,
            null,
            "DE123456789",
            paymentTermDays).Value;

    private static TransportOrder AnOrder(int sequence = 1, bool delivered = true)
    {
        DateTimeOffset pickup = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);
        Address address = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;

        TransportOrder order = TransportOrder.Create(
            OrderNumber.From(2027, sequence),
            address,
            address,
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

    private static Invoice AnInvoice(decimal vatRatePercent = 19m, int paymentTermDays = 30) =>
        Invoice.Create(InvoiceNumber.From(2027, 1), ACustomer(paymentTermDays), vatRatePercent, CreatedAt).Value;

    [Fact]
    public void Create_ValidInput_StartsAsDraftWithCustomerSnapshot()
    {
        Customer customer = ACustomer();

        Result<Invoice> result = Invoice.Create(InvoiceNumber.From(2027, 1), customer, 19m, CreatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(InvoiceStatus.Draft);
        result.Value.CustomerId.Should().Be(customer.Id);
        result.Value.CustomerName.Should().Be("Spedition Kunde GmbH");
        result.Value.BillingAddress.Should().Be(customer.Address);
        result.Value.CustomerVatId.Should().Be("DE123456789");
        result.Value.PaymentTermDays.Should().Be(30);
        result.Value.Lines.Should().BeEmpty();
        result.Value.IssuedAt.Should().BeNull();
        result.Value.DueDate.Should().BeNull();
        result.Value.PaidAt.Should().BeNull();
        result.Value.Id.Should().NotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void Create_VatRateOutOfRange_ReturnsValidationError(double rate)
    {
        Result<Invoice> result = Invoice.Create(
            InvoiceNumber.From(2027, 1), ACustomer(), (decimal)rate, CreatedAt);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Invoice.VatRateOutOfRange");
    }

    [Fact]
    public void AddLine_DeliveredOrder_AddsPositionedLineWithOrderNumberCopy()
    {
        Invoice invoice = AnInvoice();
        TransportOrder order = AnOrder();

        Result<Unit> result = invoice.AddLine(order, 450.50m);

        result.IsSuccess.Should().BeTrue();
        invoice.Lines.Should().ContainSingle();
        invoice.Lines[0].Position.Should().Be(1);
        invoice.Lines[0].TransportOrderId.Should().Be(order.Id);
        invoice.Lines[0].OrderNumber.Should().Be(order.OrderNumber.Value);
        invoice.Lines[0].Description.Should().Be($"Transport {order.OrderNumber.Value}");
        invoice.Lines[0].NetAmount.Should().Be(450.50m);
    }

    [Fact]
    public void AddLine_CustomDescription_TrimsAndKeepsIt()
    {
        Invoice invoice = AnInvoice();

        invoice.AddLine(AnOrder(), 100m, "  Sonderfahrt München–Hamburg  ");

        invoice.Lines[0].Description.Should().Be("Sonderfahrt München–Hamburg");
    }

    [Fact]
    public void AddLine_UndeliveredOrder_ReturnsConflict()
    {
        Invoice invoice = AnInvoice();

        Result<Unit> result = invoice.AddLine(AnOrder(delivered: false), 100m);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Invoice.OrderNotDelivered");
        invoice.Lines.Should().BeEmpty();
    }

    [Fact]
    public void AddLine_SameOrderTwice_ReturnsConflict()
    {
        Invoice invoice = AnInvoice();
        TransportOrder order = AnOrder();
        invoice.AddLine(order, 100m);

        Result<Unit> result = invoice.AddLine(order, 200m);

        result.Error!.Code.Should().Be("Invoice.OrderAlreadyOnInvoice");
        invoice.Lines.Should().ContainSingle();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void AddLine_NonPositiveAmount_ReturnsValidationError(double amount)
    {
        Invoice invoice = AnInvoice();

        Result<Unit> result = invoice.AddLine(AnOrder(), (decimal)amount);

        result.Error!.Code.Should().Be("Invoice.LineAmountNotPositive");
    }

    [Fact]
    public void AddLine_MoreThanTwoDecimals_ReturnsValidationError()
    {
        Invoice invoice = AnInvoice();

        Result<Unit> result = invoice.AddLine(AnOrder(), 100.555m);

        result.Error!.Code.Should().Be("Invoice.LineAmountTooPrecise");
    }

    [Fact]
    public void AddLine_IssuedInvoice_ReturnsNotEditable()
    {
        Invoice invoice = AnInvoice();
        invoice.AddLine(AnOrder(1), 100m);
        invoice.Issue(CreatedAt);

        Result<Unit> result = invoice.AddLine(AnOrder(2), 200m);

        result.Error!.Code.Should().Be("Invoice.NotEditable");
    }

    [Fact]
    public void RemoveLine_SecondOfThree_RenumbersContiguously()
    {
        Invoice invoice = AnInvoice();
        TransportOrder first = AnOrder(1);
        TransportOrder second = AnOrder(2);
        TransportOrder third = AnOrder(3);
        invoice.AddLine(first, 100m);
        invoice.AddLine(second, 200m);
        invoice.AddLine(third, 300m);

        Result<Unit> result = invoice.RemoveLine(second.Id);

        result.IsSuccess.Should().BeTrue();
        invoice.Lines.Should().HaveCount(2);
        invoice.Lines.Select(l => l.Position).Should().Equal(1, 2);
        invoice.BilledOrderIds().Should().Equal(first.Id, third.Id);
    }

    [Fact]
    public void RemoveLine_UnknownOrder_ReturnsNotFound()
    {
        Invoice invoice = AnInvoice();

        Result<Unit> result = invoice.RemoveLine(Guid.NewGuid());

        result.Error!.Code.Should().Be("Invoice.LineNotFound");
    }

    [Fact]
    public void Totals_MultipleLines_SumAndRoundVatCommercially()
    {
        Invoice invoice = AnInvoice(vatRatePercent: 19m);
        invoice.AddLine(AnOrder(1), 100.10m);
        invoice.AddLine(AnOrder(2), 0.03m);

        // 100.13 * 0.19 = 19.0247 → rounds to 19.02; gross 119.15.
        invoice.NetTotal.Should().Be(100.13m);
        invoice.VatAmount.Should().Be(19.02m);
        invoice.GrossTotal.Should().Be(119.15m);
    }

    [Fact]
    public void Totals_MidpointVat_RoundsAwayFromZero()
    {
        Invoice invoice = AnInvoice(vatRatePercent: 19m);
        invoice.AddLine(AnOrder(1), 36.50m);

        // 36.50 * 0.19 = 6.935 → kaufmännisch 6.94, not banker's 6.93 (hence 43.44 gross).
        invoice.VatAmount.Should().Be(6.94m);
        invoice.GrossTotal.Should().Be(43.44m);
    }

    [Fact]
    public void Issue_DraftWithLines_SetsIssuedAtAndDueDateFromPaymentTerm()
    {
        Invoice invoice = AnInvoice(paymentTermDays: 30);
        invoice.AddLine(AnOrder(), 100m);

        Result<Unit> result = invoice.Issue(new DateTimeOffset(2027, 3, 12, 10, 0, 0, TimeSpan.Zero));

        result.IsSuccess.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Issued);
        invoice.IssuedAt.Should().Be(new DateTimeOffset(2027, 3, 12, 10, 0, 0, TimeSpan.Zero));
        invoice.DueDate.Should().Be(new DateOnly(2027, 4, 11));
    }

    [Fact]
    public void Issue_WithoutLines_ReturnsConflict()
    {
        Invoice invoice = AnInvoice();

        Result<Unit> result = invoice.Issue(CreatedAt);

        result.Error!.Code.Should().Be("Invoice.NoLines");
        invoice.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public void Issue_AlreadyIssued_ReturnsInvalidTransition()
    {
        Invoice invoice = AnInvoice();
        invoice.AddLine(AnOrder(), 100m);
        invoice.Issue(CreatedAt);

        Result<Unit> result = invoice.Issue(CreatedAt);

        result.Error!.Code.Should().Be("Invoice.InvalidTransition");
    }

    [Fact]
    public void IsDocument_DraftInvoice_ReturnsFalse()
    {
        AnInvoice().IsDocument.Should().BeFalse();
    }

    [Fact]
    public void IsDocument_IssuedInvoice_ReturnsTrue()
    {
        Invoice invoice = AnInvoice();
        invoice.AddLine(AnOrder(), 100m);
        invoice.Issue(CreatedAt);

        invoice.IsDocument.Should().BeTrue();
    }

    [Fact]
    public void IsDocument_PaidInvoice_ReturnsTrue()
    {
        Invoice invoice = AnInvoice();
        invoice.AddLine(AnOrder(), 100m);
        invoice.Issue(CreatedAt);
        invoice.MarkPaid(CreatedAt.AddDays(5));

        invoice.IsDocument.Should().BeTrue();
    }

    [Fact]
    public void IsDocument_CancelledInvoice_ReturnsFalse()
    {
        Invoice invoice = AnInvoice();
        invoice.AddLine(AnOrder(), 100m);
        invoice.Issue(CreatedAt);
        invoice.Cancel();

        invoice.IsDocument.Should().BeFalse();
    }

    [Fact]
    public void MarkPaid_IssuedInvoice_SetsPaidAt()
    {
        Invoice invoice = AnInvoice();
        invoice.AddLine(AnOrder(), 100m);
        invoice.Issue(CreatedAt);
        DateTimeOffset paidAt = CreatedAt.AddDays(14);

        Result<Unit> result = invoice.MarkPaid(paidAt);

        result.IsSuccess.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidAt.Should().Be(paidAt);
    }

    [Fact]
    public void MarkPaid_Draft_ReturnsInvalidTransition()
    {
        Invoice invoice = AnInvoice();

        Result<Unit> result = invoice.MarkPaid(CreatedAt);

        result.Error!.Code.Should().Be("Invoice.InvalidTransition");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancel_DraftOrIssued_MovesToCancelled(bool issueFirst)
    {
        Invoice invoice = AnInvoice();
        invoice.AddLine(AnOrder(), 100m);

        if (issueFirst)
        {
            invoice.Issue(CreatedAt);
        }

        Result<Unit> result = invoice.Cancel();

        result.IsSuccess.Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
    }

    [Fact]
    public void Cancel_PaidInvoice_ReturnsInvalidTransition()
    {
        Invoice invoice = AnInvoice();
        invoice.AddLine(AnOrder(), 100m);
        invoice.Issue(CreatedAt);
        invoice.MarkPaid(CreatedAt.AddDays(1));

        Result<Unit> result = invoice.Cancel();

        result.Error!.Code.Should().Be("Invoice.InvalidTransition");
        invoice.Status.Should().Be(InvoiceStatus.Paid);
    }

    [Fact]
    public void Create_NormalisesCreatedAtToUtc()
    {
        DateTimeOffset local = new(2027, 3, 10, 9, 0, 0, TimeSpan.FromHours(2));

        Invoice invoice = Invoice.Create(InvoiceNumber.From(2027, 1), ACustomer(), 19m, local).Value;

        invoice.CreatedAt.Should().Be(local.ToUniversalTime());
        invoice.CreatedAt.Offset.Should().Be(TimeSpan.Zero);
    }
}
