using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tours;

/// <summary>
/// A day's work for one vehicle and one driver: an ordered list of stops serving a set of
/// transport orders.
/// </summary>
/// <remarks>
/// Several of this aggregate's invariants span other aggregates — capacity needs the vehicle's
/// rating and the assigned orders' cargo, the licence rule needs the driver. Those objects are
/// passed IN rather than fetched, so the domain stays free of I/O and the rules stay unit-
/// testable. The one invariant that is not here is "one tour per vehicle and driver per date":
/// that is a uniqueness question, and uniqueness cannot be decided by an object that can only
/// see itself. It lives in a database unique index (see TourConfiguration).
/// </remarks>
public sealed class Tour
{
    private readonly List<TourStop> _stops = [];

    // EF Core materialization only. Every other construction goes through Create.
    private Tour()
    {
    }

    private Tour(Guid id, DateOnly tourDate, Guid vehicleId, Guid driverId)
    {
        Id = id;
        TourDate = tourDate;
        VehicleId = vehicleId;
        DriverId = driverId;
        Status = TourStatus.Planned;
    }

    public Guid Id { get; private set; }

    public DateOnly TourDate { get; private set; }

    public Guid VehicleId { get; private set; }

    public Guid DriverId { get; private set; }

    public TourStatus Status { get; private set; }

    public IReadOnlyList<TourStop> Stops => _stops;

    public static Result<Tour> Create(DateOnly tourDate, Vehicle vehicle, Driver driver)
    {
        if (vehicle.Status != VehicleStatus.Available)
        {
            return Error.Conflict(
                "Tour.VehicleNotAvailable",
                $"Vehicle '{vehicle.LicensePlate.Value}' is '{vehicle.Status}' and cannot be assigned to a tour.");
        }

        // Driver.CanDriveOn already encodes spec 5.3's rule in full - "Status == Available and
        // LicenseValidUntil >= Tourdatum" - so it is asked once and is the only judge here.
        // The branch below does not re-decide anything; it only picks which of the two reasons
        // to name, because "this driver cannot be assigned" without saying why sends a
        // dispatcher hunting through two screens.
        if (!driver.CanDriveOn(tourDate))
        {
            return driver.Status != DriverStatus.Available
                ? Error.Conflict(
                    "Tour.DriverNotAvailable",
                    $"Driver '{driver.LastName}' is '{driver.Status}' and cannot be assigned to a tour.")
                : Error.Conflict(
                    "Tour.LicenceExpired",
                    $"The driver's licence expires on {driver.LicenseValidUntil:yyyy-MM-dd}, before the tour date {tourDate:yyyy-MM-dd}.");
        }

        return new Tour(Guid.CreateVersion7(), tourDate, vehicle.Id, driver.Id);
    }

    /// <param name="alreadyAssigned">
    /// The orders this tour already carries. Required because a tour stores order ids, not
    /// orders, and so cannot sum its own load.
    /// </param>
    public Result<Unit> AssignOrder(
        TransportOrder order,
        Vehicle vehicle,
        IReadOnlyList<TransportOrder> alreadyAssigned)
    {
        if (Status != TourStatus.Planned)
        {
            return NotEditable();
        }

        if (_stops.Any(stop => stop.TransportOrderId == order.Id))
        {
            return Error.Conflict(
                "Tour.OrderAlreadyAssigned",
                $"Order '{order.OrderNumber.Value}' is already on this tour.");
        }

        int totalWeight = alreadyAssigned.Sum(o => o.Cargo.WeightKg) + order.Cargo.WeightKg;
        if (totalWeight > vehicle.PayloadKg)
        {
            return Error.Conflict(
                "Tour.PayloadExceeded",
                $"Adding this order would load {totalWeight} kg onto a vehicle rated for {vehicle.PayloadKg} kg.");
        }

        decimal totalLoadMeters = alreadyAssigned.Sum(o => o.Cargo.LoadMeters) + order.Cargo.LoadMeters;
        if (totalLoadMeters > vehicle.LoadMeters)
        {
            return Error.Conflict(
                "Tour.LoadMetersExceeded",
                $"Adding this order would need {totalLoadMeters} load meters on a vehicle offering {vehicle.LoadMeters}.");
        }

        // Last, and deliberately: this MUTATES the order, so every cheap refusal above must
        // already have run. It also carries the spec 5.4 rule that an order belongs to at most
        // one active tour — an order another tour has planned is no longer Draft and refuses.
        Result<Unit> planned = order.MarkPlanned();
        if (!planned.IsSuccess)
        {
            return planned.Error!;
        }

        _stops.Add(TourStop.Create(_stops.Count + 1, order.Id, StopType.Pickup));
        _stops.Add(TourStop.Create(_stops.Count + 1, order.Id, StopType.Delivery));

        return Unit.Value;
    }

    public Result<Unit> RemoveOrder(TransportOrder order)
    {
        if (Status != TourStatus.Planned)
        {
            return NotEditable();
        }

        if (_stops.All(stop => stop.TransportOrderId != order.Id))
        {
            return Error.NotFound(
                "Tour.OrderNotAssigned",
                $"Order '{order.OrderNumber.Value}' is not on this tour.");
        }

        Result<Unit> returned = order.ReturnToDraft();
        if (!returned.IsSuccess)
        {
            return returned.Error!;
        }

        _stops.RemoveAll(stop => stop.TransportOrderId == order.Id);
        Renumber();

        return Unit.Value;
    }

    /// <remarks>
    /// Moves only the tour. The assigned orders are a different aggregate, so the handler
    /// transitions them — see StartTourCommandHandler for why it validates every order before
    /// moving any of them.
    /// </remarks>
    public Result<Unit> Start()
    {
        if (Status != TourStatus.Planned)
        {
            return InvalidTransition(TourStatus.InProgress);
        }

        // An empty tour is a planning mistake, not a journey: starting one would occupy a
        // vehicle and a driver for the day while carrying nothing.
        if (_stops.Count == 0)
        {
            return Error.Conflict("Tour.NoStops", "A tour without stops cannot be started.");
        }

        Status = TourStatus.InProgress;
        return Unit.Value;
    }

    public Result<Unit> Complete()
    {
        if (Status != TourStatus.InProgress)
        {
            return InvalidTransition(TourStatus.Completed);
        }

        Status = TourStatus.Completed;
        return Unit.Value;
    }

    /// <summary>The distinct order ids on this tour, in the order they are first called at.</summary>
    public IReadOnlyList<Guid> AssignedOrderIds() =>
        _stops.OrderBy(stop => stop.Sequence).Select(stop => stop.TransportOrderId).Distinct().ToList();

    // Sequences stay contiguous from 1. A gap would not break any single check, but it would
    // make "the next stop is Count + 1" wrong the moment anything relied on it.
    private void Renumber()
    {
        List<TourStop> renumbered = _stops
            .OrderBy(stop => stop.Sequence)
            .Select((stop, index) => stop.WithSequence(index + 1))
            .ToList();

        _stops.Clear();
        _stops.AddRange(renumbered);
    }

    private Result<Unit> NotEditable() => Error.Conflict(
        "Tour.NotEditable",
        $"A tour in status '{Status}' no longer accepts changes to its stops.");

    private Result<Unit> InvalidTransition(TourStatus to) => Error.Conflict(
        "Tour.InvalidTransition",
        $"A tour in status '{Status}' cannot move to '{to}'.");
}
