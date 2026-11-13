using TransBrain.Domain.Common;

namespace TransBrain.Domain.Orders;

public sealed class TransportOrder
{
    public const int MaxNotesLength = 2000;

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
        DateTimeOffset createdAt,
        Guid? consigneeLocationId,
        string? notes,
        string? confirmationNotes,
        bool requiresPartitionWall,
        bool requiresDoubleDeck)
    {
        Id = id;
        OrderNumber = orderNumber;
        Consignor = consignor;
        Consignee = consignee;
        Cargo = cargo;
        PickupWindow = pickupWindow;
        DeliveryWindow = deliveryWindow;
        CreatedAt = createdAt;
        ConsigneeLocationId = consigneeLocationId;
        Notes = notes;
        ConfirmationNotes = confirmationNotes;
        RequiresPartitionWall = requiresPartitionWall;
        RequiresDoubleDeck = requiresDoubleDeck;
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

    /// <summary>
    /// The customer delivery location the consignee was picked from, if any. The consignee
    /// address stays a copy on the order: editing the location later does not rewrite past orders.
    /// </summary>
    public Guid? ConsigneeLocationId { get; private set; }

    /// <summary>Special remarks for dispatch ("Besonderes").</summary>
    public string? Notes { get; private set; }

    /// <summary>Special remarks printed on the order confirmation ("Besonderes Auftragsbestätigung").</summary>
    public string? ConfirmationNotes { get; private set; }

    /// <summary>The load needs a vehicle with a partition wall ("Trennwand").</summary>
    public bool RequiresPartitionWall { get; private set; }

    /// <summary>The load needs a vehicle with a second loading level ("Doppelstock").</summary>
    public bool RequiresDoubleDeck { get; private set; }

    public static Result<TransportOrder> Create(
        OrderNumber orderNumber,
        Address consignor,
        Address consignee,
        Cargo cargo,
        TimeWindow pickupWindow,
        TimeWindow deliveryWindow,
        DateTimeOffset createdAt,
        Guid? consigneeLocationId = null,
        string? notes = null,
        string? confirmationNotes = null,
        bool requiresPartitionWall = false,
        bool requiresDoubleDeck = false)
    {
        Result<Unit> windows = ValidateWindows(pickupWindow, deliveryWindow);
        if (!windows.IsSuccess)
        {
            return windows.Error!;
        }

        Result<Unit> notesResult = ValidateNotes(notes, confirmationNotes);
        if (!notesResult.IsSuccess)
        {
            return notesResult.Error!;
        }

        return new TransportOrder(
            Guid.CreateVersion7(),
            orderNumber,
            consignor,
            consignee,
            cargo,
            pickupWindow,
            deliveryWindow,
            createdAt.ToUniversalTime(),
            consigneeLocationId,
            OptionalText.Normalize(notes),
            OptionalText.Normalize(confirmationNotes),
            requiresPartitionWall,
            requiresDoubleDeck);
    }

    /// <remarks>Editable only while the order is still a draft; spec §6.4.</remarks>
    public Result<TransportOrder> Update(
        Address consignor,
        Address consignee,
        Cargo cargo,
        TimeWindow pickupWindow,
        TimeWindow deliveryWindow,
        Guid? consigneeLocationId = null,
        string? notes = null,
        string? confirmationNotes = null,
        bool requiresPartitionWall = false,
        bool requiresDoubleDeck = false)
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

        Result<Unit> notesResult = ValidateNotes(notes, confirmationNotes);
        if (!notesResult.IsSuccess)
        {
            return notesResult.Error!;
        }

        Consignor = consignor;
        Consignee = consignee;
        Cargo = cargo;
        PickupWindow = pickupWindow;
        DeliveryWindow = deliveryWindow;
        ConsigneeLocationId = consigneeLocationId;
        Notes = OptionalText.Normalize(notes);
        ConfirmationNotes = OptionalText.Normalize(confirmationNotes);
        RequiresPartitionWall = requiresPartitionWall;
        RequiresDoubleDeck = requiresDoubleDeck;

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

    private static Result<Unit> ValidateNotes(string? notes, string? confirmationNotes)
    {
        if (OptionalText.Normalize(notes) is { Length: > MaxNotesLength })
        {
            return Error.Validation(
                "TransportOrder.NotesTooLong",
                $"The notes must not exceed {MaxNotesLength} characters.");
        }

        if (OptionalText.Normalize(confirmationNotes) is { Length: > MaxNotesLength })
        {
            return Error.Validation(
                "TransportOrder.ConfirmationNotesTooLong",
                $"The confirmation notes must not exceed {MaxNotesLength} characters.");
        }

        return Unit.Value;
    }
}
