using AwesomeAssertions;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.UpdateVehicle;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class UpdateVehicleCommandHandlerTests
{
    private static Vehicle ExistingVehicle(string plate = "M-AB 1234") =>
        Vehicle.Create(LicensePlate.Create(plate).Value, VehicleType.Van, 3_000, 4.0m, new DateOnly(2027, 3, 31)).Value;

    private static UpdateVehicleCommandHandler CreateHandler(
        InMemoryVehicleRepository repository,
        InMemoryCacheService cache,
        InMemoryDriverRepository? drivers = null,
        InMemoryVehicleCatalogRepository? catalog = null) =>
        new(
            repository,
            drivers ?? new InMemoryDriverRepository(),
            catalog ?? InMemoryVehicleCatalogRepository.WithStandardEntries(repository),
            cache,
            new StubCurrentUser("dispo"),
            new FixedTimeProvider());

    [Fact]
    public async Task Handle_InactiveClassTheVehicleAlreadyHas_KeepsIt()
    {
        InMemoryVehicleRepository repository = new();
        InMemoryVehicleCatalogRepository catalog = new(repository);
        catalog.Seed(VehicleCatalogKind.VehicleClass, "Silo", "Silo", isActive: false);
        catalog.Seed(VehicleCatalogKind.CarrierGroup, "Alt", "Alte Gruppe", isActive: false);
        Vehicle vehicle = Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.Van, 3_000, 4.0m, new DateOnly(2027, 3, 31),
            VehicleDetails.Create(vehicleClass: "Silo", carrierGroup: "Alt").Value).Value;
        repository.Seed(vehicle);
        UpdateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService(), catalog: catalog);

        Result<VehicleResponse> result = await handler.Handle(
            new UpdateVehicleCommand(
                vehicle.Id, "M-AB 1234", "Van", 3_000, 4.0m, new DateOnly(2027, 3, 31),
                VehicleClass: "silo", CarrierGroup: "Alt", Make: "MAN"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.VehicleClass.Should().Be("Silo");
        result.Value.CarrierGroup.Should().Be("Alt");
    }

    [Fact]
    public async Task Handle_SwitchToAnInactiveClass_ReturnsValidationErrorAndDoesNotSave()
    {
        InMemoryVehicleRepository repository = new();
        InMemoryVehicleCatalogRepository catalog = InMemoryVehicleCatalogRepository.WithStandardEntries(repository);
        catalog.Seed(VehicleCatalogKind.VehicleClass, "Silo", "Silo", isActive: false);
        Vehicle vehicle = ExistingVehicle();
        repository.Seed(vehicle);
        UpdateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService(), catalog: catalog);

        Result<VehicleResponse> result = await handler.Handle(
            new UpdateVehicleCommand(
                vehicle.Id, "M-AB 1234", "Van", 3_000, 4.0m, new DateOnly(2027, 3, 31), VehicleClass: "Silo"),
            CancellationToken.None);

        result.Error!.Failures.Should().ContainKey("VehicleClass");
        repository.SaveChangesCallCount.Should().Be(0);
        vehicle.Details.VehicleClass.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MasterDataFields_ReplacesDetailsDriverAndAuditStamp()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = ExistingVehicle();
        repository.Seed(vehicle);
        InMemoryDriverRepository drivers = new();
        Driver driver = Driver.Create("Frank", "Fahrer", [LicenseClass.CE], new DateOnly(2028, 1, 1), null).Value;
        drivers.Seed(driver);
        UpdateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService(), drivers);

        Result<VehicleResponse> result = await handler.Handle(
            new UpdateVehicleCommand(
                vehicle.Id, "M-AB 1234", "Van", 3_000, 4.0m, new DateOnly(2027, 3, 31),
                Make: "Mercedes-Benz", VehicleClass: "Tank", DefaultDriverId: driver.Id, Notes: "Neu"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        vehicle.Details.Make.Should().Be("Mercedes-Benz");
        vehicle.Details.VehicleClass.Should().Be("Tank");
        vehicle.DefaultDriverId.Should().Be(driver.Id);
        vehicle.ModifiedBy.Should().Be("dispo");
        vehicle.ModifiedAt.Should().Be(FixedTimeProvider.Default);
        result.Value.Notes.Should().Be("Neu");
    }

    [Fact]
    public async Task Handle_OmittedOptionalFields_ClearsPreviousDetails()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.Van, 3_000, 4.0m, new DateOnly(2027, 3, 31),
            VehicleDetails.Create(make: "MAN").Value).Value;
        repository.Seed(vehicle);
        UpdateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService());

        Result<VehicleResponse> result = await handler.Handle(
            new UpdateVehicleCommand(vehicle.Id, "M-AB 1234", "Van", 3_000, 4.0m, new DateOnly(2027, 3, 31)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        vehicle.Details.Should().Be(VehicleDetails.Empty);
    }

    [Fact]
    public async Task Handle_UnknownDefaultDriver_ReturnsNotFoundAndDoesNotSave()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = ExistingVehicle();
        repository.Seed(vehicle);
        UpdateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService());

        Result<VehicleResponse> result = await handler.Handle(
            new UpdateVehicleCommand(
                vehicle.Id, "M-AB 1234", "Van", 3_000, 4.0m, new DateOnly(2027, 3, 31),
                DefaultDriverId: Guid.CreateVersion7()),
            CancellationToken.None);

        result.Error!.Code.Should().Be("Driver.NotFound");
        vehicle.DefaultDriverId.Should().BeNull();
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_KnownVehicle_UpdatesFieldsAndSavesOnce()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = ExistingVehicle();
        repository.Seed(vehicle);
        InMemoryCacheService cache = new();
        UpdateVehicleCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(
            new UpdateVehicleCommand(vehicle.Id, "M-ZZ 9999", "Tractor", 24_000, 13.6m, new DateOnly(2029, 1, 1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LicensePlate.Should().Be("M-ZZ 9999");
        result.Value.Type.Should().Be("Tractor");
        result.Value.PayloadKg.Should().Be(24_000);
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownVehicle_ReturnsNotFoundAndDoesNotSave()
    {
        InMemoryVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        UpdateVehicleCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(
            new UpdateVehicleCommand(Guid.CreateVersion7(), "M-ZZ 9999", "Tractor", 24_000, 13.6m, new DateOnly(2029, 1, 1)),
            CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NonPositivePayload_LeavesVehicleUnchangedAndDoesNotSave()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = ExistingVehicle();
        repository.Seed(vehicle);
        InMemoryCacheService cache = new();
        UpdateVehicleCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(
            new UpdateVehicleCommand(vehicle.Id, "M-AB 1234", "Van", 0, 4.0m, new DateOnly(2027, 3, 31)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.PayloadKgNotPositive");
        vehicle.PayloadKg.Should().Be(3_000);
        repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PlateTakenByAnotherVehicle_ReturnsConflict()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle first = ExistingVehicle("M-AB 1234");
        Vehicle second = ExistingVehicle("M-CD 5678");
        repository.Seed(first, second);
        InMemoryCacheService cache = new();
        UpdateVehicleCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(
            new UpdateVehicleCommand(first.Id, "M-CD 5678", "Van", 3_000, 4.0m, new DateOnly(2027, 3, 31)),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Vehicle.DuplicateLicensePlate");
    }

    // This is the test excludingId exists for: updating a vehicle without changing its own
    // plate must not collide with itself via ExistsByLicensePlateAsync.
    [Fact]
    public async Task Handle_UnchangedPlateOnSameVehicle_Succeeds()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = ExistingVehicle("M-AB 1234");
        repository.Seed(vehicle);
        InMemoryCacheService cache = new();
        UpdateVehicleCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(
            new UpdateVehicleCommand(vehicle.Id, "M-AB 1234", "Tractor", 24_000, 13.6m, new DateOnly(2029, 1, 1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LicensePlate.Should().Be("M-AB 1234");
        repository.SaveChangesCallCount.Should().Be(1);
    }
}

