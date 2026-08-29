using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Tours;

public class TourTests
{
    private static readonly DateOnly TourDate = new(2027, 3, 1);

    private static Vehicle AVehicle(
        int payloadKg = 18_000,
        decimal loadMeters = 13.6m,
        bool inWorkshop = false)
    {
        Vehicle vehicle = Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value,
            VehicleType.RigidTruck,
            payloadKg,
            loadMeters,
            new DateOnly(2028, 1, 1)).Value;

        if (inWorkshop)
        {
            vehicle.SendToWorkshop();
        }

        return vehicle;
    }

    private static Driver ADriver(DateOnly? licenceUntil = null, bool available = true)
    {
        Driver driver = Driver.Create("Frank", "Fahrer", [LicenseClass.CE],
            licenceUntil ?? new DateOnly(2028, 6, 30), null).Value;

        if (!available)
        {
            driver.MarkAbsent();
        }

        return driver;
    }

    private static TransportOrder AnOrder(int weightKg = 5_000, decimal loadMeters = 4.0m)
    {
        DateTimeOffset pickup = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);
        Address address = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;

        return TransportOrder.Create(
            OrderNumber.From(2027, 1),
            address,
            address,
            Cargo.Create("Palettenware", weightKg, loadMeters).Value,
            TimeWindow.Create(pickup, pickup.AddHours(2)).Value,
            TimeWindow.Create(pickup.AddHours(4), pickup.AddHours(8)).Value,
            pickup.AddDays(-30)).Value;
    }

    private static Tour ATour(Vehicle? vehicle = null, Driver? driver = null) =>
        Tour.Create(TourDate, vehicle ?? AVehicle(), driver ?? ADriver()).Value;

    [Fact]
    public void Create_AvailableVehicleAndDriver_StartsPlannedWithNoStops()
    {
        Vehicle vehicle = AVehicle();
        Driver driver = ADriver();

        Result<Tour> result = Tour.Create(TourDate, vehicle, driver);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(TourStatus.Planned);
        result.Value.TourDate.Should().Be(TourDate);
        result.Value.VehicleId.Should().Be(vehicle.Id);
        result.Value.DriverId.Should().Be(driver.Id);
        result.Value.Stops.Should().BeEmpty();
        result.Value.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void Create_VehicleInWorkshop_ReturnsConflict()
    {
        Result<Tour> result = Tour.Create(TourDate, AVehicle(inWorkshop: true), ADriver());

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Tour.VehicleNotAvailable");
    }

    [Fact]
    public void Create_DriverNotAvailable_ReturnsConflict()
    {
        Result<Tour> result = Tour.Create(TourDate, AVehicle(), ADriver(available: false));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.DriverNotAvailable");
    }

    [Fact]
    public void Create_LicenceExpiresBeforeTourDate_ReturnsConflict()
    {
        Result<Tour> result = Tour.Create(TourDate, AVehicle(), ADriver(licenceUntil: TourDate.AddDays(-1)));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.LicenceExpired");
    }

    // The boundary the spec words as "LicenseValidUntil >= Tourdatum": a licence expiring ON the
    // tour date is still valid that day. Off by one here silently grounds a legal driver.
    [Fact]
    public void Create_LicenceExpiresExactlyOnTourDate_Succeeds()
    {
        Result<Tour> result = Tour.Create(TourDate, AVehicle(), ADriver(licenceUntil: TourDate));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AssignOrder_FirstOrder_AddsPickupThenDeliveryAndPlansTheOrder()
    {
        Tour tour = ATour();
        TransportOrder order = AnOrder();

        Result<Unit> result = tour.AssignOrder(order, AVehicle(), []);

        result.IsSuccess.Should().BeTrue();
        tour.Stops.Should().HaveCount(2);
        tour.Stops[0].Sequence.Should().Be(1);
        tour.Stops[0].StopType.Should().Be(StopType.Pickup);
        tour.Stops[0].TransportOrderId.Should().Be(order.Id);
        tour.Stops[1].Sequence.Should().Be(2);
        tour.Stops[1].StopType.Should().Be(StopType.Delivery);
        tour.Stops[1].TransportOrderId.Should().Be(order.Id);
        order.Status.Should().Be(OrderStatus.Planned);
    }

    [Fact]
    public void AssignOrder_SecondOrder_AppendsAfterTheFirstOrdersStops()
    {
        Tour tour = ATour();
        Vehicle vehicle = AVehicle();
        TransportOrder first = AnOrder();
        TransportOrder second = AnOrder();
        tour.AssignOrder(first, vehicle, []);

        tour.AssignOrder(second, vehicle, [first]);

        tour.Stops.Select(s => s.Sequence).Should().ContainInOrder(1, 2, 3, 4);
        tour.Stops[2].TransportOrderId.Should().Be(second.Id);
        tour.Stops[2].StopType.Should().Be(StopType.Pickup);
        tour.Stops[3].StopType.Should().Be(StopType.Delivery);
    }

    [Fact]
    public void AssignOrder_ExceedingPayload_ReturnsConflictAndAddsNoStops()
    {
        Vehicle vehicle = AVehicle(payloadKg: 10_000);
        Tour tour = ATour(vehicle);
        TransportOrder assigned = AnOrder(weightKg: 6_000);
        tour.AssignOrder(assigned, vehicle, []);
        TransportOrder tooHeavy = AnOrder(weightKg: 5_000);

        Result<Unit> result = tour.AssignOrder(tooHeavy, vehicle, [assigned]);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Tour.PayloadExceeded");
        tour.Stops.Should().HaveCount(2);
        tooHeavy.Status.Should().Be(OrderStatus.Draft);
    }

    // The boundary: filling the vehicle exactly to its rated payload is legal.
    [Fact]
    public void AssignOrder_FillingPayloadExactly_Succeeds()
    {
        Vehicle vehicle = AVehicle(payloadKg: 10_000);
        Tour tour = ATour(vehicle);
        TransportOrder assigned = AnOrder(weightKg: 6_000);
        tour.AssignOrder(assigned, vehicle, []);

        Result<Unit> result = tour.AssignOrder(AnOrder(weightKg: 4_000), vehicle, [assigned]);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AssignOrder_ExceedingLoadMeters_ReturnsConflict()
    {
        Vehicle vehicle = AVehicle(loadMeters: 8.0m);
        Tour tour = ATour(vehicle);
        TransportOrder assigned = AnOrder(loadMeters: 5.0m);
        tour.AssignOrder(assigned, vehicle, []);

        Result<Unit> result = tour.AssignOrder(AnOrder(loadMeters: 3.5m), vehicle, [assigned]);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.LoadMetersExceeded");
    }

    [Fact]
    public void AssignOrder_OrderAlreadyOnThisTour_ReturnsConflict()
    {
        Tour tour = ATour();
        Vehicle vehicle = AVehicle();
        TransportOrder order = AnOrder();
        tour.AssignOrder(order, vehicle, []);

        Result<Unit> result = tour.AssignOrder(order, vehicle, [order]);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.OrderAlreadyAssigned");
    }

    // Spec 5.4: an order belongs to at most one active tour. A second tour gets the refusal
    // from the order's own status machine, so no cross-tour lookup exists anywhere.
    [Fact]
    public void AssignOrder_OrderAlreadyPlannedOnAnotherTour_ReturnsConflict()
    {
        Vehicle vehicle = AVehicle();
        Tour first = ATour(vehicle);
        Tour second = ATour(vehicle);
        TransportOrder order = AnOrder();
        first.AssignOrder(order, vehicle, []);

        Result<Unit> result = second.AssignOrder(order, vehicle, []);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        second.Stops.Should().BeEmpty();
    }

    [Fact]
    public void AssignOrder_CancelledOrder_ReturnsConflictAndAddsNoStops()
    {
        Tour tour = ATour();
        TransportOrder order = AnOrder();
        order.Cancel();

        Result<Unit> result = tour.AssignOrder(order, AVehicle(), []);

        result.IsSuccess.Should().BeFalse();
        tour.Stops.Should().BeEmpty();
    }

    [Fact]
    public void AssignOrder_TourInProgress_ReturnsConflict()
    {
        Tour tour = ATour();
        Vehicle vehicle = AVehicle();
        tour.AssignOrder(AnOrder(), vehicle, []);
        tour.Start();

        Result<Unit> result = tour.AssignOrder(AnOrder(), vehicle, []);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NotEditable");
    }

    [Fact]
    public void RemoveOrder_AssignedOrder_DropsBothStopsRenumbersAndReturnsTheOrderToDraft()
    {
        Tour tour = ATour();
        Vehicle vehicle = AVehicle();
        TransportOrder first = AnOrder();
        TransportOrder second = AnOrder();
        tour.AssignOrder(first, vehicle, []);
        tour.AssignOrder(second, vehicle, [first]);

        Result<Unit> result = tour.RemoveOrder(first);

        result.IsSuccess.Should().BeTrue();
        tour.Stops.Should().HaveCount(2);
        tour.Stops.Should().OnlyContain(s => s.TransportOrderId == second.Id);
        // Renumbered contiguously - a gap would break the "pickup before delivery" ordering
        // the next assignment relies on.
        tour.Stops.Select(s => s.Sequence).Should().ContainInOrder(1, 2);
        first.Status.Should().Be(OrderStatus.Draft);
        second.Status.Should().Be(OrderStatus.Planned);
    }

    [Fact]
    public void RemoveOrder_OrderNotOnTheTour_ReturnsNotFound()
    {
        Tour tour = ATour();

        Result<Unit> result = tour.RemoveOrder(AnOrder());

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Tour.OrderNotAssigned");
    }

    [Fact]
    public void RemoveOrder_TourInProgress_ReturnsConflict()
    {
        Tour tour = ATour();
        TransportOrder order = AnOrder();
        tour.AssignOrder(order, AVehicle(), []);
        tour.Start();

        Result<Unit> result = tour.RemoveOrder(order);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NotEditable");
    }

    [Fact]
    public void Start_PlannedTourWithStops_BecomesInProgress()
    {
        Tour tour = ATour();
        tour.AssignOrder(AnOrder(), AVehicle(), []);

        Result<Unit> result = tour.Start();

        result.IsSuccess.Should().BeTrue();
        tour.Status.Should().Be(TourStatus.InProgress);
    }

    // An empty tour is a planning mistake, not a journey. Starting one would move a vehicle and
    // a driver into InProgress for the day while carrying nothing.
    [Fact]
    public void Start_TourWithoutStops_ReturnsConflict()
    {
        Tour tour = ATour();

        Result<Unit> result = tour.Start();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.NoStops");
        tour.Status.Should().Be(TourStatus.Planned);
    }

    [Fact]
    public void Start_AlreadyInProgress_ReturnsConflict()
    {
        Tour tour = ATour();
        tour.AssignOrder(AnOrder(), AVehicle(), []);
        tour.Start();

        Result<Unit> result = tour.Start();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.InvalidTransition");
    }

    [Fact]
    public void Complete_InProgressTour_BecomesCompleted()
    {
        Tour tour = ATour();
        tour.AssignOrder(AnOrder(), AVehicle(), []);
        tour.Start();

        Result<Unit> result = tour.Complete();

        result.IsSuccess.Should().BeTrue();
        tour.Status.Should().Be(TourStatus.Completed);
    }

    [Fact]
    public void Complete_PlannedTour_ReturnsConflict()
    {
        Tour tour = ATour();
        tour.AssignOrder(AnOrder(), AVehicle(), []);

        Result<Unit> result = tour.Complete();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.InvalidTransition");
        tour.Status.Should().Be(TourStatus.Planned);
    }

    [Fact]
    public void Complete_AlreadyCompleted_ReturnsConflict()
    {
        Tour tour = ATour();
        tour.AssignOrder(AnOrder(), AVehicle(), []);
        tour.Start();
        tour.Complete();

        Result<Unit> result = tour.Complete();

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.InvalidTransition");
    }
}
