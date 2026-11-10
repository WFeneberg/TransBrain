using AwesomeAssertions;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class CreateVehicleCommandHandlerTests
{
    private static CreateVehicleCommand ValidCommand => new("M-AB 1234", "Tractor", 24_000, 13.6m, new DateOnly(2027, 3, 31));

    private static CreateVehicleCommandHandler CreateHandler(
        InMemoryVehicleRepository repository,
        InMemoryCacheService cache,
        InMemoryDriverRepository? drivers = null,
        string? user = "dispo",
        InMemoryVehicleCatalogRepository? catalog = null) =>
        new(
            repository,
            drivers ?? new InMemoryDriverRepository(),
            catalog ?? InMemoryVehicleCatalogRepository.WithStandardEntries(repository),
            cache,
            new StubCurrentUser(user),
            new FixedTimeProvider());

    [Fact]
    public async Task Handle_MasterDataFields_RoundTripsThemIntoTheResponse()
    {
        InMemoryVehicleRepository repository = new();
        InMemoryDriverRepository drivers = new();
        Driver driver = Driver.Create("Frank", "Fahrer", [LicenseClass.CE], new DateOnly(2028, 1, 1), null).Value;
        drivers.Seed(driver);
        CreateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService(), drivers);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with
            {
                IsLicensePlateConfidential = true,
                Make = " MAN ",
                Model = "TGX 18.510",
                ManufacturedOn = new DateOnly(2021, 5, 17),
                PowerKw = 375,
                VehicleClass = "refrigerated",
                CarrierGroup = "Nahverkehr",
                DefaultDriverId = driver.Id,
                ChassisNumber = "wma06xzz1lm123456",
                EngineNumber = "D2676LF",
                LengthMm = 6_100,
                WidthMm = 2_550,
                HeightMm = 3_950,
                CurbWeightKg = 8_200,
                GrossWeightKg = 18_000,
                MaxFifthWheelLoadKg = 11_500,
                Notes = "Retarder defekt",
            },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        VehicleResponse response = result.Value;
        response.IsLicensePlateConfidential.Should().BeTrue();
        response.Make.Should().Be("MAN");
        response.Model.Should().Be("TGX 18.510");
        response.ManufacturedOn.Should().Be(new DateOnly(2021, 5, 1));
        response.PowerKw.Should().Be(375);
        response.VehicleClass.Should().Be("Refrigerated");
        response.CarrierGroup.Should().Be("Nahverkehr");
        response.DefaultDriverId.Should().Be(driver.Id);
        response.ChassisNumber.Should().Be("WMA06XZZ1LM123456");
        response.EngineNumber.Should().Be("D2676LF");
        response.LengthMm.Should().Be(6_100);
        response.WidthMm.Should().Be(2_550);
        response.HeightMm.Should().Be(3_950);
        response.CurbWeightKg.Should().Be(8_200);
        response.GrossWeightKg.Should().Be(18_000);
        response.MaxFifthWheelLoadKg.Should().Be(11_500);
        response.Notes.Should().Be("Retarder defekt");
        response.ModifiedBy.Should().Be("dispo");
        response.ModifiedAt.Should().Be(FixedTimeProvider.Default);
    }

    [Fact]
    public async Task Handle_Equipment_PersistsItAndReturnsItInTheResponse()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService());
        VehicleEquipmentData equipment = new(
            HasPartitionWall: true,
            IsDangerousGoodsApproved: true,
            HasLargeLadder: true,
            TankVolumeLiters: 1_200,
            InteriorLengthMm: 13_600,
            InteriorWidthMm: 2_480,
            InteriorHeightMm: 2_700,
            LashingStrapCount: 20,
            PalletCount: 0,
            MarbleRackCount: 3,
            MarbleRackMaterial: "Aluminium");

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { Equipment = equipment }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Equipment.Should().Be(equipment);
        Vehicle stored = repository.Vehicles.Should().ContainSingle().Subject;
        stored.Equipment.MarbleRackMaterial.Should().Be(MarbleRackMaterial.Aluminium);
        stored.Equipment.PalletCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_NoEquipment_ReturnsEmptyEquipment()
    {
        CreateVehicleCommandHandler handler = CreateHandler(new InMemoryVehicleRepository(), new InMemoryCacheService());

        Result<VehicleResponse> result = await handler.Handle(ValidCommand, CancellationToken.None);

        result.Value.Equipment.Should().Be(new VehicleEquipmentData());
    }

    [Fact]
    public async Task Handle_EquipmentOutOfRange_ReturnsDomainErrorAndPersistsNothing()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService());

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { Equipment = new VehicleEquipmentData(TankVolumeLiters: 0) }, CancellationToken.None);

        result.Error!.Code.Should().Be("Vehicle.TankVolumeOutOfRange");
        repository.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnknownDefaultDriver_ReturnsNotFoundAndPersistsNothing()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService());

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { DefaultDriverId = Guid.CreateVersion7() }, CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Driver.NotFound");
        repository.Vehicles.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Spaceship")]
    [InlineData("42")]
    public async Task Handle_UnknownVehicleClass_ReturnsValidationError(string vehicleClass)
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService());

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { VehicleClass = vehicleClass }, CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Failures.Should().ContainKey("VehicleClass");
        repository.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_UnknownCarrierGroup_ReturnsValidationErrorKeyedCarrierGroup()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService());

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { CarrierGroup = "Unbekannt" }, CancellationToken.None);

        result.Error!.Failures.Should().ContainKey("CarrierGroup");
        result.Error.Failures!["CarrierGroup"].Should().ContainSingle().Which.Should().Contain("carrier group");
        repository.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_InactiveVehicleClass_ReturnsValidationError()
    {
        InMemoryVehicleRepository repository = new();
        InMemoryVehicleCatalogRepository catalog = new(repository);
        catalog.Seed(VehicleCatalogKind.VehicleClass, "Silo", "Silo", isActive: false);
        CreateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService(), catalog: catalog);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { VehicleClass = "Silo" }, CancellationToken.None);

        result.Error!.Failures.Should().ContainKey("VehicleClass");
        repository.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_BlankCatalogCodes_StoresNoClassAndNoCarrierGroup()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService());

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { VehicleClass = " ", CarrierGroup = "" }, CancellationToken.None);

        result.Value.VehicleClass.Should().BeNull();
        result.Value.CarrierGroup.Should().BeNull();
    }

    [Fact]
    public async Task Handle_GrossWeightNotAboveCurbWeight_ReturnsDomainValidationError()
    {
        InMemoryVehicleRepository repository = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, new InMemoryCacheService());

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { CurbWeightKg = 8_000, GrossWeightKg = 8_000 }, CancellationToken.None);

        result.Error!.Code.Should().Be("Vehicle.GrossWeightNotAboveCurbWeight");
        repository.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ValidCommand_PersistsVehicleAndReturnsResponse()
    {
        InMemoryVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(ValidCommand, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LicensePlate.Should().Be("M-AB 1234");
        result.Value.Status.Should().Be("Available");
        repository.Vehicles.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_DuplicateLicensePlate_ReturnsConflictError()
    {
        InMemoryVehicleRepository repository = new();
        repository.Seed(Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 3, 31)).Value);
        InMemoryCacheService cache = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(ValidCommand, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Vehicle.DuplicateLicensePlate");
        repository.Vehicles.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_UnknownVehicleType_ReturnsValidationError()
    {
        InMemoryVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { Type = "Spaceship" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.UnknownType");
    }

    [Theory]
    [InlineData("RigidTruck")]
    [InlineData("Van")]
    public async Task Handle_RetiredVehicleType_ReturnsValidationError(string type)
    {
        InMemoryVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { Type = type }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.UnknownType");
        repository.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NumericUndefinedVehicleType_ReturnsValidationError()
    {
        // Enum.TryParse alone happily parses a numeric string into the underlying integer
        // value even when no VehicleType member defines it (e.g. (VehicleType)99), which would
        // then pass Vehicle.Create and be persisted as the literal string "99". This guards the
        // fix (Enum.TryParse combined with Enum.IsDefined) that rejects it before it gets there.
        InMemoryVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { Type = "99" }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.UnknownType");
        repository.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_InvalidLicensePlate_ReturnsDomainValidationError()
    {
        InMemoryVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { LicensePlate = "   " }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("LicensePlate.Empty");
    }

    [Fact]
    public async Task Handle_NonPositivePayload_ReturnsDomainValidationError()
    {
        InMemoryVehicleRepository repository = new();
        InMemoryCacheService cache = new();
        CreateVehicleCommandHandler handler = CreateHandler(repository, cache);

        Result<VehicleResponse> result = await handler.Handle(
            ValidCommand with { PayloadKg = 0 }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.PayloadKgNotPositive");
    }
}

