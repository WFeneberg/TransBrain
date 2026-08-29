using AwesomeAssertions;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.CreateTour;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Tours;

public class CreateTourCommandHandlerTests
{
    private static readonly DateOnly TourDate = new(2027, 3, 1);

    private static Vehicle AVehicle() => Vehicle.Create(
        LicensePlate.Create("M-AB 1234").Value,
        VehicleType.RigidTruck,
        18_000,
        13.6m,
        new DateOnly(2028, 1, 1)).Value;

    private static Driver ADriver() => Driver.Create(
        "Frank", "Fahrer", [LicenseClass.CE], new DateOnly(2028, 6, 30), null).Value;

    [Fact]
    public async Task Handle_AvailableVehicleAndDriver_PersistsTourAndReturnsResponse()
    {
        InMemoryVehicleRepository vehicles = new();
        InMemoryDriverRepository drivers = new();
        InMemoryTourRepository tours = new();
        Vehicle vehicle = AVehicle();
        Driver driver = ADriver();
        vehicles.Seed(vehicle);
        drivers.Seed(driver);
        CreateTourCommandHandler handler = new(tours, vehicles, drivers);

        Result<TourResponse> result = await handler.Handle(
            new CreateTourCommand(TourDate, vehicle.Id, driver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Planned");
        result.Value.TourDate.Should().Be(TourDate);
        result.Value.VehicleLicensePlate.Should().Be(vehicle.LicensePlate.Value);
        result.Value.DriverName.Should().Be("Fahrer, Frank");
        result.Value.Stops.Should().BeEmpty();
        // The capacity headroom a dispatcher needs before assigning anything.
        result.Value.VehiclePayloadKg.Should().Be(18_000);
        result.Value.TotalWeightKg.Should().Be(0);
        tours.Tours.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_UnknownVehicle_ReturnsNotFoundAndPersistsNothing()
    {
        InMemoryVehicleRepository vehicles = new();
        InMemoryDriverRepository drivers = new();
        InMemoryTourRepository tours = new();
        Driver driver = ADriver();
        drivers.Seed(driver);
        CreateTourCommandHandler handler = new(tours, vehicles, drivers);

        Result<TourResponse> result = await handler.Handle(
            new CreateTourCommand(TourDate, Guid.CreateVersion7(), driver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Vehicle.NotFound");
        tours.Tours.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnknownDriver_ReturnsNotFoundAndPersistsNothing()
    {
        InMemoryVehicleRepository vehicles = new();
        InMemoryDriverRepository drivers = new();
        InMemoryTourRepository tours = new();
        Vehicle vehicle = AVehicle();
        vehicles.Seed(vehicle);
        CreateTourCommandHandler handler = new(tours, vehicles, drivers);

        Result<TourResponse> result = await handler.Handle(
            new CreateTourCommand(TourDate, vehicle.Id, Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Driver.NotFound");
        tours.Tours.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DriverLicenceExpiredBeforeTourDate_ReturnsDomainConflict()
    {
        InMemoryVehicleRepository vehicles = new();
        InMemoryDriverRepository drivers = new();
        InMemoryTourRepository tours = new();
        Vehicle vehicle = AVehicle();
        Driver driver = Driver.Create(
            "Frank", "Fahrer", [LicenseClass.CE], TourDate.AddDays(-1), null).Value;
        vehicles.Seed(vehicle);
        drivers.Seed(driver);
        CreateTourCommandHandler handler = new(tours, vehicles, drivers);

        Result<TourResponse> result = await handler.Handle(
            new CreateTourCommand(TourDate, vehicle.Id, driver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.LicenceExpired");
        tours.Tours.Should().BeEmpty();
    }

    // The double-booking rule lives in a database unique index, so the handler's only job is to
    // pass the repository's Conflict through unchanged rather than swallow or reword it.
    [Fact]
    public async Task Handle_RepositoryReportsDoubleBooking_ReturnsThatConflict()
    {
        InMemoryVehicleRepository vehicles = new();
        InMemoryDriverRepository drivers = new();
        Vehicle vehicle = AVehicle();
        Driver driver = ADriver();
        vehicles.Seed(vehicle);
        drivers.Seed(driver);
        InMemoryTourRepository tours = new()
        {
            AddConflict = Error.Conflict("Tour.VehicleAlreadyBooked", "already booked")
        };
        CreateTourCommandHandler handler = new(tours, vehicles, drivers);

        Result<TourResponse> result = await handler.Handle(
            new CreateTourCommand(TourDate, vehicle.Id, driver.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Tour.VehicleAlreadyBooked");
    }
}
