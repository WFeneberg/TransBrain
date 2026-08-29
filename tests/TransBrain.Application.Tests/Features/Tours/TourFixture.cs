using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Tours;

/// <summary>
/// A tour with its vehicle and driver already seeded into the four fakes every tour handler
/// needs. Shared across the tour handler test classes because building it inline five times
/// would bury each test's actual arrangement under twelve lines of scaffolding.
/// </summary>
internal sealed record TourFixture(
    InMemoryTourRepository Tours,
    InMemoryVehicleRepository Vehicles,
    InMemoryDriverRepository Drivers,
    InMemoryTransportOrderRepository Orders,
    Tour Tour,
    Vehicle Vehicle,
    Driver Driver)
{
    public static readonly DateOnly TourDate = new(2027, 3, 1);

    public static TourFixture Create(
        int payloadKg = 18_000,
        decimal loadMeters = 13.6m,
        string? driverExternalUserId = null)
    {
        InMemoryTourRepository tours = new();
        InMemoryVehicleRepository vehicles = new();
        InMemoryDriverRepository drivers = new();
        InMemoryTransportOrderRepository orders = new();

        Vehicle vehicle = AVehicle(payloadKg, loadMeters);
        Driver driver = ADriver(driverExternalUserId);
        vehicles.Seed(vehicle);
        drivers.Seed(driver);

        Tour tour = Tour.Create(TourDate, vehicle, driver).Value;
        tours.Seed(tour);

        return new TourFixture(tours, vehicles, drivers, orders, tour, vehicle, driver);
    }

    public static Vehicle AVehicle(int payloadKg = 18_000, decimal loadMeters = 13.6m) =>
        Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value,
            VehicleType.RigidTruck,
            payloadKg,
            loadMeters,
            new DateOnly(2028, 1, 1)).Value;

    public static Driver ADriver(string? externalUserId = null) =>
        Driver.Create("Frank", "Fahrer", [LicenseClass.CE], new DateOnly(2028, 6, 30), externalUserId).Value;

    public static TransportOrder AnOrder(int weightKg = 5_000, decimal loadMeters = 4.0m, int sequence = 1)
    {
        DateTimeOffset pickup = new(2027, 3, 1, 8, 0, 0, TimeSpan.Zero);
        Address consignor = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;
        Address consignee = Address.Create("Empfaenger AG", "Bahnhofstr. 2", "10115", "Berlin", "DE").Value;

        return TransportOrder.Create(
            OrderNumber.From(2027, sequence),
            consignor,
            consignee,
            Cargo.Create("Palettenware", weightKg, loadMeters).Value,
            TimeWindow.Create(pickup, pickup.AddHours(2)).Value,
            TimeWindow.Create(pickup.AddHours(4), pickup.AddHours(8)).Value,
            pickup.AddDays(-30)).Value;
    }

    /// <summary>Seeds an order and puts it on the tour, as AssignOrder's handler would.</summary>
    public TransportOrder AssignedOrder(int weightKg = 5_000, decimal loadMeters = 4.0m, int sequence = 1)
    {
        TransportOrder order = AnOrder(weightKg, loadMeters, sequence);
        Orders.Seed(order);

        List<TransportOrder> alreadyAssigned = Tour.AssignedOrderIds()
            .Select(id => Orders.Orders.Single(o => o.Id == id))
            .ToList();

        Tour.AssignOrder(order, Vehicle, alreadyAssigned);
        return order;
    }
}
