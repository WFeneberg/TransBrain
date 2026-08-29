using TransBrain.Domain.Common;

namespace TransBrain.Domain.Orders;

public sealed class TransportOrder
{
    // EF Core materialization only. Every other construction goes through Create.
    private TransportOrder()
    {
        OrderNumber = null!;
        Consignor = null!;
        Consignee = null!;
        Cargo = null!;
        PickupWindow = null!;
        DeliveryWindow = null!;
    }

    private TransportOrder(
        Guid id,
        OrderNumber orderNumber,
        Address consignor,
        Address consignee,
        Cargo cargo,
        TimeWindow pickupWindow,
        TimeWindow deliveryWindow,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrderNumber = orderNumber;
        Consignor = consignor;
        Consignee = consignee;
        Cargo = cargo;
        PickupWindow = pickupWindow;
        DeliveryWindow = deliveryWindow;
        CreatedAt = createdAt;
        Status = OrderStatus.Draft;
    }

    public Guid Id { get; private set; }

    public OrderNumber OrderNumber { get; private set; }

    public Address Consignor { get; private set; }

    public Address Consignee { get; private set; }

    public Cargo Cargo { get; private set; }

    public TimeWindow PickupWindow { get; private set; }

    public TimeWindow DeliveryWindow { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<TransportOrder> Create(
        OrderNumber orderNumber,
        Address consignor,
        Address consignee,
        Cargo cargo,
        TimeWindow pickupWindow,
        TimeWindow deliveryWindow,
        DateTimeOffset createdAt)
    {
        Result<Unit> windows = ValidateWindows(pickupWindow, deliveryWindow);
        if (!windows.IsSuccess)
        {
            return windows.Error!;
        }

        return new TransportOrder(
            Guid.CreateVersion7(),
            orderNumber,
            consignor,
            consignee,
            cargo,
            pickupWindow,
            deliveryWindow,
            createdAt.ToUniversalTime());
    }

    /// <remarks>Editable only while the order is still a draft; spec §6.4.</remarks>
    public Result<TransportOrder> Update(
        Address consignor,
        Address consignee,
        Cargo cargo,
        TimeWindow pickupWindow,
        TimeWindow deliveryWindow)
    {
        if (Status != OrderStatus.Draft)
        {
            return Error.Conflict(
                "TransportOrder.NotEditable",
                $"An order in status '{Status}' can no longer be edited.");
        }

        Result<Unit> windows = ValidateWindows(pickupWindow, deliveryWindow);
        if (!windows.IsSuccess)
        {
            return windows.Error!;
        }

        Consignor = consignor;
        Consignee = consignee;
        Cargo = cargo;
        PickupWindow = pickupWindow;
        DeliveryWindow = deliveryWindow;

        return this;
    }

    public Result<Unit> MarkPlanned() => Transition(OrderStatus.Draft, OrderStatus.Planned);

    /// <remarks>
    /// The reverse of <see cref="MarkPlanned"/>, for an order taken off a tour before that tour
    /// started. Spec §5.4's diagram does not draw this arrow, but §6.4 requires a RemoveOrder
    /// slice: without a way back, a removed order would be stranded in Planned with no tour —
    /// neither assignable to another tour nor editable. Deliberately NOT reachable from
    /// InTransit: once the goods are moving, taking the order off a tour cannot un-move them.
    /// </remarks>
    public Result<Unit> ReturnToDraft() => Transition(OrderStatus.Planned, OrderStatus.Draft);

    public Result<Unit> MarkInTransit() => Transition(OrderStatus.Planned, OrderStatus.InTransit);

    public Result<Unit> MarkDelivered() => Transition(OrderStatus.InTransit, OrderStatus.Delivered);

    /// <remarks>
    /// Cancellable from Draft or Planned only. Once the goods are moving the order describes
    /// something that is physically happening, so spec §5.4 forbids cancelling from InTransit,
    /// and Delivered is final.
    /// </remarks>
    public Result<Unit> Cancel()
    {
        if (Status is not (OrderStatus.Draft or OrderStatus.Planned))
        {
            return InvalidTransition(OrderStatus.Cancelled);
        }

        Status = OrderStatus.Cancelled;
        return Unit.Value;
    }

    /// <remarks>
    /// Unlike Driver.MarkAvailable and Vehicle.ReturnToService, which are silent no-ops, an
    /// invalid workflow transition returns a Conflict. A status toggle that quietly declines is
    /// reasonable; a workflow step that quietly declines would let a caller believe the order
    /// advanced when it did not.
    /// </remarks>
    private Result<Unit> Transition(OrderStatus from, OrderStatus to)
    {
        if (Status != from)
        {
            return InvalidTransition(to);
        }

        Status = to;
        return Unit.Value;
    }

    private Result<Unit> InvalidTransition(OrderStatus to) => Error.Conflict(
        "TransportOrder.InvalidTransition",
        $"An order in status '{Status}' cannot move to '{to}'.");

    private static Result<Unit> ValidateWindows(TimeWindow pickupWindow, TimeWindow deliveryWindow)
    {
        if (pickupWindow.To > deliveryWindow.From)
        {
            return Error.Validation(
                "TransportOrder.DeliveryBeforePickupEnds",
                "The delivery window must not start before the pickup window ends.");
        }

        return Unit.Value;
    }
}
