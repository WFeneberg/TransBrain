using AwesomeAssertions;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Application.Features.Vehicles.UpdateVehicle;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class VehicleCommercialDataHandlerTests
{
    private static CreateVehicleCommand ValidCreate => new("M-AB 1234", "Tractor", 24_000, 13.6m, new DateOnly(2027, 3, 31));

    private static CreateVehicleCommandHandler CreateHandler(
        InMemoryVehicleRepository repository, InMemoryVehicleCatalogRepository? catalog = null) =>
        new(
            repository,
            new InMemoryDriverRepository(),
            catalog ?? InMemoryVehicleCatalogRepository.WithStandardEntries(repository),
            new InMemoryCacheService(),
            new StubCurrentUser("dispo"),
            new FixedTimeProvider());

    private static UpdateVehicleCommandHandler UpdateHandler(
        InMemoryVehicleRepository repository, InMemoryVehicleCatalogRepository catalog) =>
        new(
            repository,
            new InMemoryDriverRepository(),
            catalog,
            new InMemoryCacheService(),
            new StubCurrentUser("dispo"),
            new FixedTimeProvider());

    private static UpdateVehicleCommand UpdateOf(Vehicle vehicle, VehicleCommercialData? commercial, string? dataSheet = null) =>
        new(vehicle.Id, "M-AB 1234", "Tractor", 24_000, 13.6m, new DateOnly(2027, 3, 31), Commercial: commercial, DataSheet: dataSheet);

    [Fact]
    public async Task Create_CommercialDataAndDataSheet_PersistsThemWithCanonicalInsurerCodes()
    {
        InMemoryVehicleRepository repository = new();
        VehicleCommercialData commercial = new(
            PurchasePrice: 125_000.50m,
            PurchasedOn: new DateOnly(2023, 3, 15),
            PurchaseOdometerKm: 12_000,
            FirstRegisteredOn: new DateOnly(2022, 11, 30),
            UsefulLifeYears: 8,
            UsefulLifeKm: 900_000,
            CostCenter: " KST-4711 ",
            VehicleInsurer: "allianz",
            VehicleInsurancePolicyNumbers: "POL-1\nPOL-2",
            TransportInsurer: "Uniqa",
            TransportInsurancePolicyNumbers: "TR-9");
        const string sheet = "Motor:  D13K\n  Leistung: 368 kW";

        Result<VehicleResponse> result = await CreateHandler(repository).Handle(
            ValidCreate with { Commercial = commercial, DataSheet = sheet }, CancellationToken.None);

        result.Value.Commercial.Should().Be(commercial with
        {
            CostCenter = "KST-4711", VehicleInsurer = "ALLIANZ", TransportInsurer = "UNIQA"
        });
        result.Value.DataSheet.Should().Be(sheet);
        Vehicle stored = repository.Vehicles.Single();
        stored.Commercial.VehicleInsurer.Should().Be("ALLIANZ");
        stored.Details.DataSheet.Should().Be(sheet);
    }

    [Fact]
    public async Task Create_NoCommercialData_ReturnsEmptyCommercialData()
    {
        Result<VehicleResponse> result = await CreateHandler(new InMemoryVehicleRepository())
            .Handle(ValidCreate, CancellationToken.None);

        result.Value.Commercial.Should().Be(new VehicleCommercialData());
        result.Value.DataSheet.Should().BeNull();
    }

    [Fact]
    public async Task Create_UnknownInsurer_ReturnsFailureKeyedByDottedFieldAndPersistsNothing()
    {
        InMemoryVehicleRepository repository = new();

        Result<VehicleResponse> result = await CreateHandler(repository).Handle(
            ValidCreate with { Commercial = new VehicleCommercialData(TransportInsurer: "NOPE") },
            CancellationToken.None);

        result.Error!.Failures!["Commercial.TransportInsurer"].Should().ContainSingle()
            .Which.Should().Contain("insurance company");
        repository.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Create_VehicleClassCodeAsInsurer_IsRejected()
    {
        Result<VehicleResponse> result = await CreateHandler(new InMemoryVehicleRepository()).Handle(
            ValidCreate with { Commercial = new VehicleCommercialData(VehicleInsurer: "Tank") },
            CancellationToken.None);

        result.Error!.Failures.Should().ContainKey("Commercial.VehicleInsurer");
    }

    [Fact]
    public async Task Create_CommercialValueOutOfRange_ReturnsDomainError()
    {
        InMemoryVehicleRepository repository = new();

        Result<VehicleResponse> result = await CreateHandler(repository).Handle(
            ValidCreate with { Commercial = new VehicleCommercialData(UsefulLifeYears: 0) }, CancellationToken.None);

        result.Error!.Code.Should().Be("Vehicle.UsefulLifeOutOfRange");
        repository.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_InactiveInsurerTheVehicleAlreadyHas_KeepsIt()
    {
        InMemoryVehicleRepository repository = new();
        InMemoryVehicleCatalogRepository catalog = InMemoryVehicleCatalogRepository.WithStandardEntries(repository);
        catalog.Seed(VehicleCatalogKind.InsuranceCompany, "OLDINS", "Alte Versicherung", isActive: false);
        Vehicle vehicle = Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 3, 31),
            commercial: VehicleCommercialInfo.Create(vehicleInsurer: "OLDINS").Value).Value;
        repository.Seed(vehicle);

        Result<VehicleResponse> kept = await UpdateHandler(repository, catalog).Handle(
            UpdateOf(vehicle, new VehicleCommercialData(VehicleInsurer: "oldins")), CancellationToken.None);
        Result<VehicleResponse> switched = await UpdateHandler(repository, catalog).Handle(
            UpdateOf(vehicle, new VehicleCommercialData(VehicleInsurer: "ALLIANZ", TransportInsurer: "OLDINS")),
            CancellationToken.None);

        kept.Value.Commercial.VehicleInsurer.Should().Be("OLDINS");
        switched.Error!.Failures.Should().ContainKey("Commercial.TransportInsurer");
        vehicle.Commercial.TransportInsurer.Should().BeNull();
    }

    [Fact]
    public async Task Update_NullCommercialAndDataSheet_ClearsThem()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 3, 31),
            VehicleDetails.Create(dataSheet: "old").Value,
            commercial: VehicleCommercialInfo.Create(costCenter: "KST-1").Value).Value;
        repository.Seed(vehicle);

        Result<VehicleResponse> result = await UpdateHandler(
                repository, InMemoryVehicleCatalogRepository.WithStandardEntries(repository))
            .Handle(UpdateOf(vehicle, commercial: null), CancellationToken.None);

        result.Value.Commercial.Should().Be(new VehicleCommercialData());
        result.Value.DataSheet.Should().BeNull();
        vehicle.Commercial.Should().Be(VehicleCommercialInfo.Empty);
        repository.SaveChangesCallCount.Should().Be(1);
    }
}
