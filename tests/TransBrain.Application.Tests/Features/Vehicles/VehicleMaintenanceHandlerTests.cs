using AwesomeAssertions;
using FluentValidation.Results;
using TransBrain.Application.Features.Vehicles.VehicleMaintenances;
using TransBrain.Application.Features.Vehicles.VehicleMaintenances.AddVehicleMaintenance;
using TransBrain.Application.Features.Vehicles.VehicleMaintenances.DeleteVehicleMaintenance;
using TransBrain.Application.Features.Vehicles.VehicleMaintenances.ListVehicleMaintenances;
using TransBrain.Application.Features.Vehicles.VehicleMaintenances.UpdateVehicleMaintenance;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class VehicleMaintenanceHandlerTests
{
    private static readonly DateTimeOffset Earlier = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Performed = new(2026, 11, 10);

    private readonly InMemoryVehicleRepository _repository = new();
    private readonly Vehicle _vehicle;

    public VehicleMaintenanceHandlerTests()
    {
        _vehicle = Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 3, 31)).Value;
        _repository.Seed(_vehicle);
    }

    private AddVehicleMaintenanceCommandHandler AddHandler() =>
        new(_repository, new StubCurrentUser("admin"), new FixedTimeProvider());

    private UpdateVehicleMaintenanceCommandHandler UpdateHandler() =>
        new(_repository, new StubCurrentUser("admin"), new FixedTimeProvider());

    private VehicleMaintenance SeedMaintenance(
        DateOnly performedOn,
        string description = "Arbeit",
        Guid? vehicleId = null,
        DateTimeOffset? modifiedAt = null)
    {
        VehicleMaintenance maintenance = VehicleMaintenance.Create(
            vehicleId ?? _vehicle.Id, VehicleMaintenanceKind.Service, performedOn, description,
            null, null, null, null, "someone", modifiedAt ?? Earlier).Value;
        _repository.SeedMaintenances(maintenance);
        return maintenance;
    }

    [Fact]
    public async Task Add_ValidMaintenance_PersistsItWithParsedKindAndAuditStamp()
    {
        Result<VehicleMaintenanceResponse> result = await AddHandler().Handle(
            new AddVehicleMaintenanceCommand(
                _vehicle.Id, "service", Performed, "Ölwechsel", "Werkstatt Nord", 250_000, 849.90m,
                new DateOnly(2027, 11, 10)),
            CancellationToken.None);

        result.Value.Should().BeEquivalentTo(new
        {
            VehicleId = _vehicle.Id,
            Kind = "Service",
            PerformedOn = Performed,
            Description = "Ölwechsel",
            Workshop = "Werkstatt Nord",
            OdometerKm = 250_000,
            Cost = 849.90m,
            NextDueOn = new DateOnly(2027, 11, 10),
            ModifiedAt = FixedTimeProvider.Default,
            ModifiedBy = "admin",
        });
        _repository.Maintenances.Should().ContainSingle().Which.Id.Should().Be(result.Value.Id);
    }

    [Fact]
    public async Task Add_UnknownVehicle_ReturnsNotFound()
    {
        Result<VehicleMaintenanceResponse> result = await AddHandler().Handle(
            new AddVehicleMaintenanceCommand(Guid.CreateVersion7(), "Service", Performed, "x"),
            CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Add_UnknownKind_ReturnsFailureKeyedByKind()
    {
        Result<VehicleMaintenanceResponse> result = await AddHandler().Handle(
            new AddVehicleMaintenanceCommand(_vehicle.Id, "Lackieren", Performed, "x"), CancellationToken.None);

        result.Error!.Failures!["Kind"].Should().ContainSingle().Which.Should().Contain("maintenance kind");
        _repository.Maintenances.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_NextDueBeforePerformed_ReturnsDomainErrorAndDoesNotPersist()
    {
        Result<VehicleMaintenanceResponse> result = await AddHandler().Handle(
            new AddVehicleMaintenanceCommand(
                _vehicle.Id, "Service", Performed, "x", NextDueOn: Performed.AddDays(-1)),
            CancellationToken.None);

        result.Error!.Code.Should().Be("VehicleMaintenance.NextDueBeforePerformed");
        _repository.Maintenances.Should().BeEmpty();
    }

    [Fact]
    public async Task Update_ExistingMaintenance_ReplacesValuesAndRestamps()
    {
        VehicleMaintenance maintenance = SeedMaintenance(Performed);

        Result<VehicleMaintenanceResponse> result = await UpdateHandler().Handle(
            new UpdateVehicleMaintenanceCommand(
                _vehicle.Id, maintenance.Id, "repair", Performed.AddDays(1), "Turbolader getauscht", "Nord"),
            CancellationToken.None);

        result.Value.Kind.Should().Be("Repair");
        result.Value.PerformedOn.Should().Be(Performed.AddDays(1));
        result.Value.Description.Should().Be("Turbolader getauscht");
        result.Value.Workshop.Should().Be("Nord");
        result.Value.ModifiedBy.Should().Be("admin");
        result.Value.ModifiedAt.Should().Be(FixedTimeProvider.Default);
        _repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Update_MaintenanceOfAnotherVehicle_ReturnsNotFound()
    {
        VehicleMaintenance foreign = SeedMaintenance(Performed, vehicleId: Guid.CreateVersion7());

        Result<VehicleMaintenanceResponse> result = await UpdateHandler().Handle(
            new UpdateVehicleMaintenanceCommand(_vehicle.Id, foreign.Id, "Service", Performed, "x"),
            CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Update_UnknownKind_ReturnsFailureKeyedByKindAndDoesNotSave()
    {
        VehicleMaintenance maintenance = SeedMaintenance(Performed);

        Result<VehicleMaintenanceResponse> result = await UpdateHandler().Handle(
            new UpdateVehicleMaintenanceCommand(_vehicle.Id, maintenance.Id, "NOPE", Performed, "x"),
            CancellationToken.None);

        result.Error!.Failures.Should().ContainKey("Kind");
        maintenance.Kind.Should().Be(VehicleMaintenanceKind.Service);
        _repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Update_InvalidData_ReturnsDomainErrorAndDoesNotSave()
    {
        VehicleMaintenance maintenance = SeedMaintenance(Performed, "Ölwechsel");

        Result<VehicleMaintenanceResponse> result = await UpdateHandler().Handle(
            new UpdateVehicleMaintenanceCommand(_vehicle.Id, maintenance.Id, "Service", Performed, " "),
            CancellationToken.None);

        result.Error!.Code.Should().Be("VehicleMaintenance.DescriptionRequired");
        maintenance.Description.Should().Be("Ölwechsel");
        _repository.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_ExistingMaintenance_RemovesIt()
    {
        VehicleMaintenance maintenance = SeedMaintenance(Performed);

        Result<Unit> result = await new DeleteVehicleMaintenanceCommandHandler(_repository).Handle(
            new DeleteVehicleMaintenanceCommand(_vehicle.Id, maintenance.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _repository.Maintenances.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_UnknownMaintenance_ReturnsNotFound()
    {
        Result<Unit> result = await new DeleteVehicleMaintenanceCommandHandler(_repository).Handle(
            new DeleteVehicleMaintenanceCommand(_vehicle.Id, Guid.CreateVersion7()), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task List_Maintenances_ReturnsLatestFirstThenLatestChangeFirst()
    {
        SeedMaintenance(new DateOnly(2026, 1, 5), "älteste");
        SeedMaintenance(new DateOnly(2026, 6, 1), "gleicher Tag, früher geändert", modifiedAt: Earlier);
        SeedMaintenance(new DateOnly(2026, 6, 1), "gleicher Tag, später geändert", modifiedAt: Earlier.AddHours(1));
        SeedMaintenance(new DateOnly(2026, 11, 1), "neueste");
        SeedMaintenance(new DateOnly(2026, 12, 1), "anderes Fahrzeug", vehicleId: Guid.CreateVersion7());

        Result<VehicleMaintenanceResponse[]> result = await new ListVehicleMaintenancesQueryHandler(_repository)
            .Handle(new ListVehicleMaintenancesQuery(_vehicle.Id), CancellationToken.None);

        result.Value.Select(m => m.Description).Should().Equal(
            "neueste", "gleicher Tag, später geändert", "gleicher Tag, früher geändert", "älteste");
    }

    [Fact]
    public async Task List_UnknownVehicle_ReturnsNotFound()
    {
        Result<VehicleMaintenanceResponse[]> result = await new ListVehicleMaintenancesQueryHandler(_repository)
            .Handle(new ListVehicleMaintenancesQuery(Guid.CreateVersion7()), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public void Validator_InvalidFields_ReportsEachField()
    {
        ValidationResult result = new AddVehicleMaintenanceCommandValidator().Validate(
            new AddVehicleMaintenanceCommand(
                _vehicle.Id, "NOPE", new DateOnly(1899, 1, 1), "",
                new string('w', VehicleMaintenance.MaxWorkshopLength + 1), -1, -0.01m));

        result.Errors.Select(e => e.PropertyName).Distinct().Should().BeEquivalentTo(
            ["Kind", "PerformedOn", "Description", "Workshop", "OdometerKm", "Cost"]);
    }

    [Fact]
    public void Validator_NextDueBeforePerformed_ReportsNextDueOn()
    {
        ValidationResult result = new UpdateVehicleMaintenanceCommandValidator().Validate(
            new UpdateVehicleMaintenanceCommand(
                _vehicle.Id, Guid.CreateVersion7(), "Service", Performed, "x", NextDueOn: Performed.AddDays(-1)));

        result.Errors.Should().ContainSingle().Which.PropertyName.Should().Be("NextDueOn");
    }

    [Fact]
    public void Validator_RequiredFieldsOnly_IsValid()
    {
        new AddVehicleMaintenanceCommandValidator().Validate(
                new AddVehicleMaintenanceCommand(_vehicle.Id, "TireChange", Performed, "Reifen gewechselt"))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteVehicle_RemovesItsMaintenances()
    {
        SeedMaintenance(Performed);

        await _repository.RemoveAsync(_vehicle, CancellationToken.None);

        _repository.Maintenances.Should().BeEmpty();
    }
}
