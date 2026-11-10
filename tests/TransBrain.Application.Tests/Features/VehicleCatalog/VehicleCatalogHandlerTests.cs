using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Application.Features.VehicleCatalog.CreateVehicleCatalogEntry;
using TransBrain.Application.Features.VehicleCatalog.DeleteVehicleCatalogEntry;
using TransBrain.Application.Features.VehicleCatalog.ListVehicleCatalog;
using TransBrain.Application.Features.VehicleCatalog.UpdateVehicleCatalogEntry;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.VehicleCatalog;

public class VehicleCatalogHandlerTests
{
    private static Vehicle VehicleWith(string plate, string? vehicleClass = null, string? carrierGroup = null) =>
        Vehicle.Create(
            LicensePlate.Create(plate).Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 3, 31),
            VehicleDetails.Create(vehicleClass: vehicleClass, carrierGroup: carrierGroup).Value).Value;

    [Fact]
    public async Task List_EntriesOfOneKind_ReturnsThemOrderedByNameWithUsageCounts()
    {
        InMemoryVehicleRepository vehicles = new();
        vehicles.Seed(
            VehicleWith("M-A 1", "Tank", "Nahverkehr"),
            VehicleWith("M-A 2", "Tank"),
            VehicleWith("M-A 3", "Refrigerated"));
        InMemoryVehicleCatalogRepository catalog = InMemoryVehicleCatalogRepository.WithStandardEntries(vehicles);

        Result<IReadOnlyList<VehicleCatalogEntryResponse>> result = await new ListVehicleCatalogQueryHandler(catalog)
            .Handle(new ListVehicleCatalogQuery(VehicleCatalogKind.VehicleClass), CancellationToken.None);

        result.Value.Select(e => e.Name).Should().Equal("Kühlkoffer", "Plane", "Tank");
        result.Value.Select(e => e.UsageCount).Should().Equal(1, 0, 2);
        result.Value.Should().AllSatisfy(e => e.Kind.Should().Be("VehicleClass"));
    }

    [Fact]
    public async Task Create_NewCode_PersistsActiveEntry()
    {
        InMemoryVehicleCatalogRepository catalog = new();

        Result<VehicleCatalogEntryResponse> result = await new CreateVehicleCatalogEntryCommandHandler(catalog).Handle(
            new CreateVehicleCatalogEntryCommand(VehicleCatalogKind.CarrierGroup, " NV ", "Nahverkehr", true),
            CancellationToken.None);

        result.Value.Code.Should().Be("NV");
        result.Value.Kind.Should().Be("CarrierGroup");
        result.Value.UsageCount.Should().Be(0);
        catalog.Entries.Should().ContainSingle().Which.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_CodeThatExistsInOtherCase_ReturnsConflict()
    {
        InMemoryVehicleCatalogRepository catalog = InMemoryVehicleCatalogRepository.WithStandardEntries();
        int before = catalog.Entries.Count;

        Result<VehicleCatalogEntryResponse> result = await new CreateVehicleCatalogEntryCommandHandler(catalog).Handle(
            new CreateVehicleCatalogEntryCommand(VehicleCatalogKind.VehicleClass, "TANK", "Tanker", true),
            CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("VehicleCatalog.DuplicateCode");
        catalog.Entries.Should().HaveCount(before);
    }

    [Fact]
    public async Task Create_SameCodeInTheOtherCatalog_IsAllowed()
    {
        InMemoryVehicleCatalogRepository catalog = InMemoryVehicleCatalogRepository.WithStandardEntries();

        Result<VehicleCatalogEntryResponse> result = await new CreateVehicleCatalogEntryCommandHandler(catalog).Handle(
            new CreateVehicleCatalogEntryCommand(VehicleCatalogKind.CarrierGroup, "Tank", "Tankgruppe", true),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Create_InvalidCode_ReturnsDomainValidationError()
    {
        InMemoryVehicleCatalogRepository catalog = new();

        Result<VehicleCatalogEntryResponse> result = await new CreateVehicleCatalogEntryCommandHandler(catalog).Handle(
            new CreateVehicleCatalogEntryCommand(VehicleCatalogKind.CarrierGroup, "a b", "Name", true),
            CancellationToken.None);

        result.Error!.Code.Should().Be("VehicleCatalog.InvalidCode");
        catalog.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_ExistingEntry_ChangesNameAndActiveAndReportsUsage()
    {
        InMemoryVehicleRepository vehicles = new();
        vehicles.Seed(VehicleWith("M-A 1", "Tank"));
        InMemoryVehicleCatalogRepository catalog = new(vehicles);
        VehicleCatalogEntry tank = catalog.Seed(VehicleCatalogKind.VehicleClass, "Tank", "Tank");

        Result<VehicleCatalogEntryResponse> result = await new UpdateVehicleCatalogEntryCommandHandler(catalog).Handle(
            new UpdateVehicleCatalogEntryCommand(VehicleCatalogKind.VehicleClass, tank.Id, "Tankwagen", false),
            CancellationToken.None);

        result.Value.Name.Should().Be("Tankwagen");
        result.Value.IsActive.Should().BeFalse();
        result.Value.Code.Should().Be("Tank");
        result.Value.UsageCount.Should().Be(1);
        catalog.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Update_EntryOfTheOtherKind_ReturnsNotFound()
    {
        InMemoryVehicleCatalogRepository catalog = new();
        VehicleCatalogEntry tank = catalog.Seed(VehicleCatalogKind.VehicleClass, "Tank", "Tank");

        Result<VehicleCatalogEntryResponse> result = await new UpdateVehicleCatalogEntryCommandHandler(catalog).Handle(
            new UpdateVehicleCatalogEntryCommand(VehicleCatalogKind.CarrierGroup, tank.Id, "X", true),
            CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        tank.Name.Should().Be("Tank");
    }

    [Fact]
    public async Task Update_EmptyName_ReturnsValidationErrorAndDoesNotSave()
    {
        InMemoryVehicleCatalogRepository catalog = new();
        VehicleCatalogEntry tank = catalog.Seed(VehicleCatalogKind.VehicleClass, "Tank", "Tank");

        Result<VehicleCatalogEntryResponse> result = await new UpdateVehicleCatalogEntryCommandHandler(catalog).Handle(
            new UpdateVehicleCatalogEntryCommand(VehicleCatalogKind.VehicleClass, tank.Id, " ", true),
            CancellationToken.None);

        result.Error!.Code.Should().Be("VehicleCatalog.InvalidName");
        catalog.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_UnusedEntry_RemovesIt()
    {
        InMemoryVehicleCatalogRepository catalog = new(new InMemoryVehicleRepository());
        VehicleCatalogEntry group = catalog.Seed(VehicleCatalogKind.CarrierGroup, "NV", "Nahverkehr");

        Result<Unit> result = await new DeleteVehicleCatalogEntryCommandHandler(catalog).Handle(
            new DeleteVehicleCatalogEntryCommand(VehicleCatalogKind.CarrierGroup, group.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        catalog.Entries.Should().BeEmpty();
        catalog.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Delete_EntryInUse_ReturnsConflictAndKeepsIt()
    {
        InMemoryVehicleRepository vehicles = new();
        vehicles.Seed(VehicleWith("M-A 1", carrierGroup: "NV"), VehicleWith("M-A 2", carrierGroup: "NV"));
        InMemoryVehicleCatalogRepository catalog = new(vehicles);
        VehicleCatalogEntry group = catalog.Seed(VehicleCatalogKind.CarrierGroup, "NV", "Nahverkehr");

        Result<Unit> result = await new DeleteVehicleCatalogEntryCommandHandler(catalog).Handle(
            new DeleteVehicleCatalogEntryCommand(VehicleCatalogKind.CarrierGroup, group.Id), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("VehicleCatalog.InUse");
        result.Error.Message.Should().Contain("2 vehicle(s)");
        catalog.Entries.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_UnknownId_ReturnsNotFound()
    {
        InMemoryVehicleCatalogRepository catalog = new();

        Result<Unit> result = await new DeleteVehicleCatalogEntryCommandHandler(catalog).Handle(
            new DeleteVehicleCatalogEntryCommand(VehicleCatalogKind.VehicleClass, Guid.CreateVersion7()),
            CancellationToken.None);

        result.Error!.Code.Should().Be("VehicleCatalog.NotFound");
    }

    [Fact]
    public async Task List_InsuranceCompanies_CountsAVehicleOnceEvenWhenItUsesTheInsurerTwice()
    {
        InMemoryVehicleRepository vehicles = new();
        vehicles.Seed(
            VehicleInsuredBy("M-A 1", "ALLIANZ", "ALLIANZ"),
            VehicleInsuredBy("M-A 2", "UNIQA", "ALLIANZ"),
            VehicleInsuredBy("M-A 3", null, null));
        InMemoryVehicleCatalogRepository catalog = InMemoryVehicleCatalogRepository.WithStandardEntries(vehicles);

        Result<IReadOnlyList<VehicleCatalogEntryResponse>> result = await new ListVehicleCatalogQueryHandler(catalog)
            .Handle(new ListVehicleCatalogQuery(VehicleCatalogKind.InsuranceCompany), CancellationToken.None);

        result.Value.ToDictionary(e => e.Code, e => e.UsageCount)
            .Should().BeEquivalentTo(new Dictionary<string, int> { ["ALLIANZ"] = 2, ["UNIQA"] = 1 });
    }

    [Fact]
    public async Task Delete_DocumentTypeInUse_ReturnsConflict()
    {
        InMemoryVehicleRepository vehicles = new();
        Vehicle vehicle = VehicleWith("M-A 1");
        vehicles.Seed(vehicle);
        vehicles.SeedDocuments(VehicleDocument.Create(
            vehicle.Id, "ADR", null, null, null, "admin", DateTimeOffset.UnixEpoch).Value);
        InMemoryVehicleCatalogRepository catalog = InMemoryVehicleCatalogRepository.WithStandardEntries(vehicles);
        VehicleCatalogEntry adr = catalog.Entries.Single(e => e.Code == "ADR");

        Result<Unit> result = await new DeleteVehicleCatalogEntryCommandHandler(catalog).Handle(
            new DeleteVehicleCatalogEntryCommand(VehicleCatalogKind.DocumentType, adr.Id), CancellationToken.None);

        result.Error!.Code.Should().Be("VehicleCatalog.InUse");
        catalog.Entries.Should().Contain(adr);
    }

    private static Vehicle VehicleInsuredBy(string plate, string? vehicleInsurer, string? transportInsurer) =>
        Vehicle.Create(
            LicensePlate.Create(plate).Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 3, 31),
            commercial: VehicleCommercialInfo.Create(
                vehicleInsurer: vehicleInsurer, transportInsurer: transportInsurer).Value).Value;

    [Fact]
    public void CreateValidator_InvalidFields_ReportsCodeAndName()
    {
        ValidationResult result = new CreateVehicleCatalogEntryCommandValidator().Validate(
            new CreateVehicleCatalogEntryCommand(VehicleCatalogKind.VehicleClass, "a/b", "", true));

        result.Errors.Select(e => e.PropertyName).Distinct().Should().BeEquivalentTo(["Code", "Name"]);
    }

    [Fact]
    public void UpdateValidator_TooLongName_ReportsName()
    {
        ValidationResult result = new UpdateVehicleCatalogEntryCommandValidator().Validate(
            new UpdateVehicleCatalogEntryCommand(
                VehicleCatalogKind.VehicleClass, Guid.CreateVersion7(),
                new string('n', VehicleCatalogEntry.MaxNameLength + 1), true));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("Name");
    }
}
