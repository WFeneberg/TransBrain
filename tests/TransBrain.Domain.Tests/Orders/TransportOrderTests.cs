using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Domain.Tests.Orders;

public class TransportOrderTests
{
    private static readonly DateTimeOffset CreatedAt = new(2027, 2, 1, 10, 0, 0, TimeSpan.Zero);

    private static Address AnAddress(string name) =>
        Address.Create(name, "Hauptstr. 1", "80331", "München", "DE").Value;

    private static Cargo AnyCargo() => Cargo.Create("Palettenware", 12_000, 8.4m).Value;

    private static TimeWindow Window(int startHour, int endHour) => TimeWindow.Create(
        new DateTimeOffset(2027, 3, 1, startHour, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2027, 3, 1, endHour, 0, 0, TimeSpan.Zero)).Value;

    private static TransportOrder AnOrder() => TransportOrder.Create(
        OrderNumber.From(2027, 1),
        AnAddress("Absender GmbH"),
        AnAddress("Empfänger AG"),
        AnyCargo(),
        Window(8, 10),
        Window(12, 16),
        CreatedAt).Value;

    [Fact]
    public void Create_ValidArguments_ReturnsDraftOrderWithIdentity()
    {
        Result<TransportOrder> result = TransportOrder.Create(
            OrderNumber.From(2027, 7),
            AnAddress("Absender GmbH"),
            AnAddress("Empfänger AG"),
            AnyCargo(),
            Window(8, 10),
            Window(12, 16),
            CreatedAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.OrderNumber.Value.Should().Be("TB-2027-00007");
        result.Value.Status.Should().Be(OrderStatus.Draft);
        result.Value.CreatedAt.Should().Be(CreatedAt);
    }

    [Fact]
    public void Create_DeliveryWindowStartsBeforePickupEnds_ReturnsValidationError()
    {
        Result<TransportOrder> result = TransportOrder.Create(
            OrderNumber.From(2027, 1),
            AnAddress("Absender GmbH"),
            AnAddress("Empfänger AG"),
            AnyCargo(),
            Window(8, 14),
            Window(12, 16),
            CreatedAt);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("TransportOrder.DeliveryBeforePickupEnds");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_DeliveryWindowStartsExactlyWhenPickupEnds_IsAccepted()
    {
        Result<TransportOrder> result = TransportOrder.Create(
            OrderNumber.From(2027, 1),
            AnAddress("Absender GmbH"),
            AnAddress("Empfänger AG"),
            AnyCargo(),
            Window(8, 12),
            Window(12, 16),
            CreatedAt);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void MarkPlanned_DraftOrder_MovesToPlanned()
    {
        TransportOrder order = AnOrder();

        Result<Unit> result = order.MarkPlanned();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Planned);
    }

    [Fact]
    public void MarkPlanned_AlreadyPlanned_ReturnsConflictAndLeavesStatusUnchanged()
    {
        TransportOrder order = AnOrder();
        order.MarkPlanned();

        Result<Unit> result = order.MarkPlanned();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("TransportOrder.InvalidTransition");
        order.Status.Should().Be(OrderStatus.Planned);
    }

    [Fact]
    public void MarkInTransit_PlannedOrder_MovesToInTransit()
    {
        TransportOrder order = AnOrder();
        order.MarkPlanned();

        Result<Unit> result = order.MarkInTransit();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.InTransit);
    }

    [Fact]
    public void MarkInTransit_DraftOrder_ReturnsConflict()
    {
        TransportOrder order = AnOrder();

        Result<Unit> result = order.MarkInTransit();

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public void MarkDelivered_InTransitOrder_MovesToDelivered()
    {
        TransportOrder order = AnOrder();
        order.MarkPlanned();
        order.MarkInTransit();

        Result<Unit> result = order.MarkDelivered();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void MarkDelivered_PlannedOrder_ReturnsConflict()
    {
        TransportOrder order = AnOrder();
        order.MarkPlanned();

        Result<Unit> result = order.MarkDelivered();

        result.Error!.Type.Should().Be(ErrorType.Conflict);
    }

    [Fact]
    public void Cancel_DraftOrder_MovesToCancelled()
    {
        TransportOrder order = AnOrder();

        Result<Unit> result = order.Cancel();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_PlannedOrder_MovesToCancelled()
    {
        TransportOrder order = AnOrder();
        order.MarkPlanned();

        Result<Unit> result = order.Cancel();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_InTransitOrder_ReturnsConflict()
    {
        TransportOrder order = AnOrder();
        order.MarkPlanned();
        order.MarkInTransit();

        Result<Unit> result = order.Cancel();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        order.Status.Should().Be(OrderStatus.InTransit);
    }

    [Fact]
    public void Cancel_DeliveredOrder_ReturnsConflict()
    {
        TransportOrder order = AnOrder();
        order.MarkPlanned();
        order.MarkInTransit();
        order.MarkDelivered();

        Result<Unit> result = order.Cancel();

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void MarkPlanned_CancelledOrder_ReturnsConflict()
    {
        TransportOrder order = AnOrder();
        order.Cancel();

        Result<Unit> result = order.MarkPlanned();

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void MarkPlanned_DeliveredOrder_ReturnsConflictAndLeavesStatusUnchanged()
    {
        TransportOrder order = AnOrder();
        order.MarkPlanned();
        order.MarkInTransit();
        order.MarkDelivered();

        Result<Unit> result = order.MarkPlanned();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        order.Status.Should().Be(OrderStatus.Delivered);
    }

    [Fact]
    public void MarkInTransit_CancelledOrder_ReturnsConflictAndLeavesStatusUnchanged()
    {
        TransportOrder order = AnOrder();
        order.Cancel();

        Result<Unit> result = order.MarkInTransit();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void MarkDelivered_DraftOrder_ReturnsConflictAndLeavesStatusUnchanged()
    {
        TransportOrder order = AnOrder();

        Result<Unit> result = order.MarkDelivered();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        order.Status.Should().Be(OrderStatus.Draft);
    }

    [Fact]
    public void Cancel_CancelledOrder_ReturnsConflictAndLeavesStatusUnchanged()
    {
        TransportOrder order = AnOrder();
        order.Cancel();

        Result<Unit> result = order.Cancel();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Update_DraftOrder_ReplacesEveryEditableField()
    {
        TransportOrder order = AnOrder();
        Address newConsignor = AnAddress("Neuer Absender");

        Result<TransportOrder> result = order.Update(
            newConsignor,
            AnAddress("Empfänger AG"),
            Cargo.Create("Stückgut", 500, 1.2m).Value,
            Window(6, 9),
            Window(11, 15));

        result.IsSuccess.Should().BeTrue();
        order.Consignor.Should().Be(newConsignor);
        order.Cargo.WeightKg.Should().Be(500);
    }

    [Fact]
    public void Update_PlannedOrder_ReturnsConflictAndLeavesOrderUnchanged()
    {
        TransportOrder order = AnOrder();
        order.MarkPlanned();

        Result<TransportOrder> result = order.Update(
            AnAddress("Neuer Absender"),
            AnAddress("Empfänger AG"),
            Cargo.Create("Stückgut", 500, 1.2m).Value,
            Window(6, 9),
            Window(11, 15));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("TransportOrder.NotEditable");
        order.Consignor.Name.Should().Be("Absender GmbH");
        order.Cargo.WeightKg.Should().Be(12_000);
    }

    [Fact]
    public void Update_WindowsThatOverlap_ReturnsValidationErrorAndLeavesOrderUnchanged()
    {
        TransportOrder order = AnOrder();

        Result<TransportOrder> result = order.Update(
            AnAddress("Neuer Absender"),
            AnAddress("Empfänger AG"),
            AnyCargo(),
            Window(8, 14),
            Window(12, 16));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("TransportOrder.DeliveryBeforePickupEnds");
        order.Consignor.Name.Should().Be("Absender GmbH");
    }
}
