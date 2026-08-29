using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Tours;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Api.IntegrationTests;

public class TourDoubleBookingTests(TransBrainApiFactory factory) : IClassFixture<TransBrainApiFactory>
{
    private static Vehicle AVehicle(string plate) => Vehicle.Create(
        LicensePlate.Create(plate).Value, VehicleType.RigidTruck, 18_000, 13.6m,
        new DateOnly(2028, 1, 1)).Value;

    private static Driver ADriver(string lastName) => Driver.Create(
        "Frank", lastName, [LicenseClass.CE], new DateOnly(2098, 6, 30), null).Value;

    [Fact]
    public async Task AddAsync_SecondTourForTheSameVehicleAndDate_ReturnsConflict()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IVehicleRepository vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        IDriverRepository drivers = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        ITourRepository tours = scope.ServiceProvider.GetRequiredService<ITourRepository>();

        DateOnly date = new(2097, 5, 1);
        Vehicle vehicle = AVehicle("M-DB 1001");
        Driver firstDriver = ADriver("DoppeltEins");
        Driver secondDriver = ADriver("DoppeltZwei");
        await vehicles.AddAsync(vehicle, CancellationToken.None);
        await drivers.AddAsync(firstDriver, CancellationToken.None);
        await drivers.AddAsync(secondDriver, CancellationToken.None);

        await tours.AddAsync(Tour.Create(date, vehicle, firstDriver).Value, CancellationToken.None);

        // Same vehicle, same date, a different driver: the vehicle index must be what refuses.
        Result<Tour> second = await tours.AddAsync(
            Tour.Create(date, vehicle, secondDriver).Value, CancellationToken.None);

        second.IsSuccess.Should().BeFalse();
        second.Error!.Type.Should().Be(ErrorType.Conflict);
        second.Error.Code.Should().Be("Tour.VehicleAlreadyBooked");
    }

    [Fact]
    public async Task AddAsync_SecondTourForTheSameDriverAndDate_ReturnsConflict()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IVehicleRepository vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        IDriverRepository drivers = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        ITourRepository tours = scope.ServiceProvider.GetRequiredService<ITourRepository>();

        DateOnly date = new(2097, 6, 1);
        Vehicle firstVehicle = AVehicle("M-DB 2001");
        Vehicle secondVehicle = AVehicle("M-DB 2002");
        Driver driver = ADriver("DoppeltDrei");
        await vehicles.AddAsync(firstVehicle, CancellationToken.None);
        await vehicles.AddAsync(secondVehicle, CancellationToken.None);
        await drivers.AddAsync(driver, CancellationToken.None);

        await tours.AddAsync(Tour.Create(date, firstVehicle, driver).Value, CancellationToken.None);

        Result<Tour> second = await tours.AddAsync(
            Tour.Create(date, secondVehicle, driver).Value, CancellationToken.None);

        second.IsSuccess.Should().BeFalse();
        second.Error!.Code.Should().Be("Tour.DriverAlreadyBooked");
    }

    [Fact]
    public async Task AddAsync_SameVehicleOnADifferentDate_Succeeds()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IVehicleRepository vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        IDriverRepository drivers = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        ITourRepository tours = scope.ServiceProvider.GetRequiredService<ITourRepository>();

        Vehicle vehicle = AVehicle("M-DB 3001");
        Driver driver = ADriver("DoppeltVier");
        await vehicles.AddAsync(vehicle, CancellationToken.None);
        await drivers.AddAsync(driver, CancellationToken.None);

        await tours.AddAsync(
            Tour.Create(new DateOnly(2097, 7, 1), vehicle, driver).Value, CancellationToken.None);

        Result<Tour> next = await tours.AddAsync(
            Tour.Create(new DateOnly(2097, 7, 2), vehicle, driver).Value, CancellationToken.None);

        next.IsSuccess.Should().BeTrue();
    }

    // Proves the owned stop collection round-trips: sequence, type and order all survive, and
    // the aggregate rebuilds them through its private backing field rather than a public setter.
    [Fact]
    public async Task GetByIdAsync_AfterAssigningAnOrder_ReloadsBothStopsInSequence()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IVehicleRepository vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        IDriverRepository drivers = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        ITransportOrderRepository orders = scope.ServiceProvider.GetRequiredService<ITransportOrderRepository>();
        ITourRepository tours = scope.ServiceProvider.GetRequiredService<ITourRepository>();

        Vehicle vehicle = AVehicle("M-DB 4001");
        Driver driver = ADriver("DoppeltFuenf");
        await vehicles.AddAsync(vehicle, CancellationToken.None);
        await drivers.AddAsync(driver, CancellationToken.None);

        // A real, persisted order: TransportOrder.Create assigns its own id, and widening the
        // domain's API just so a test could choose one would be the tail wagging the dog.
        //
        // Consignor and consignee are separate Address INSTANCES on purpose. Handing the same
        // object to two owned navigations makes EF throw "The property 'X' belongs to the type
        // 'TransportOrder.Consignor#Address' but is being used with an instance of type
        // 'TransportOrder.Consignee#Address'" - a confusing message for a simple cause. Equal
        // VALUES are fine (OrderEndpointsTests pins that a same-site shipment works); it is
        // instance sharing that EF rejects.
        DateTimeOffset pickup = new(2097, 8, 1, 8, 0, 0, TimeSpan.Zero);
        Address consignor = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;
        Address consignee = Address.Create("Empfaenger AG", "Bahnhofstr. 2", "10115", "Berlin", "DE").Value;
        TransportOrder order = TransportOrder.Create(
            OrderNumber.From(2097, 41),
            consignor,
            consignee,
            Cargo.Create("Palettenware", 5_000, 4.0m).Value,
            TimeWindow.Create(pickup, pickup.AddHours(2)).Value,
            TimeWindow.Create(pickup.AddHours(4), pickup.AddHours(8)).Value,
            pickup.AddDays(-30)).Value;
        await orders.AddAsync(order, CancellationToken.None);

        Tour tour = Tour.Create(new DateOnly(2097, 8, 1), vehicle, driver).Value;
        tour.AssignOrder(order, vehicle, []);
        await tours.AddAsync(tour, CancellationToken.None);

        Tour? reloaded = await tours.GetByIdAsync(tour.Id, CancellationToken.None);

        reloaded!.Stops.Should().HaveCount(2);
        reloaded.Stops[0].Sequence.Should().Be(1);
        reloaded.Stops[0].StopType.Should().Be(StopType.Pickup);
        reloaded.Stops[0].TransportOrderId.Should().Be(order.Id);
        reloaded.Stops[1].Sequence.Should().Be(2);
        reloaded.Stops[1].StopType.Should().Be(StopType.Delivery);
    }

    // The renumber path: removing the first of two orders must leave contiguous sequences in the
    // database too, not only in memory. This is the case the stop key design had to accommodate.
    [Fact]
    public async Task SaveChangesAsync_AfterRemovingAnOrder_ReloadsRenumberedStops()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        IVehicleRepository vehicles = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();
        IDriverRepository drivers = scope.ServiceProvider.GetRequiredService<IDriverRepository>();
        ITransportOrderRepository orders = scope.ServiceProvider.GetRequiredService<ITransportOrderRepository>();
        ITourRepository tours = scope.ServiceProvider.GetRequiredService<ITourRepository>();

        Vehicle vehicle = AVehicle("M-DB 5001");
        Driver driver = ADriver("DoppeltSechs");
        await vehicles.AddAsync(vehicle, CancellationToken.None);
        await drivers.AddAsync(driver, CancellationToken.None);

        TransportOrder first = await PersistOrderAsync(orders, 51);
        TransportOrder second = await PersistOrderAsync(orders, 52);

        Tour tour = Tour.Create(new DateOnly(2097, 9, 1), vehicle, driver).Value;
        tour.AssignOrder(first, vehicle, []);
        tour.AssignOrder(second, vehicle, [first]);
        await tours.AddAsync(tour, CancellationToken.None);

        tour.RemoveOrder(first);
        await tours.SaveChangesAsync(CancellationToken.None);

        Tour? reloaded = await tours.GetByIdAsync(tour.Id, CancellationToken.None);

        reloaded!.Stops.Should().HaveCount(2);
        reloaded.Stops.Select(s => s.Sequence).Should().ContainInOrder(1, 2);
        reloaded.Stops.Should().OnlyContain(s => s.TransportOrderId == second.Id);
    }

    private static async Task<TransportOrder> PersistOrderAsync(ITransportOrderRepository orders, int sequence)
    {
        DateTimeOffset pickup = new(2097, 9, 1, 8, 0, 0, TimeSpan.Zero);
        Address consignor = Address.Create("Absender GmbH", "Hauptstr. 1", "80331", "München", "DE").Value;
        Address consignee = Address.Create("Empfaenger AG", "Bahnhofstr. 2", "10115", "Berlin", "DE").Value;

        TransportOrder order = TransportOrder.Create(
            OrderNumber.From(2097, sequence),
            consignor,
            consignee,
            Cargo.Create("Palettenware", 5_000, 4.0m).Value,
            TimeWindow.Create(pickup, pickup.AddHours(2)).Value,
            TimeWindow.Create(pickup.AddHours(4), pickup.AddHours(8)).Value,
            pickup.AddDays(-30)).Value;

        await orders.AddAsync(order, CancellationToken.None);
        return order;
    }
}
