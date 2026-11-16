using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Vehicles;

public class VehicleMaintenanceTests
{
    private static readonly Guid VehicleId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Performed = new(2026, 11, 10);

    [Fact]
    public void Create_ValidData_KeepsValuesAndStamps()
    {
        VehicleMaintenance maintenance = VehicleMaintenance.Create(
            VehicleId,
            VehicleMaintenanceKind.Service,
            Performed,
            " Ölwechsel und Filter ",
            " Werkstatt Nord ",
            250_000,
            849.90m,
            new DateOnly(2027, 11, 10),
            "admin",
            Now).Value;

        maintenance.Id.Should().NotBeEmpty();
        maintenance.VehicleId.Should().Be(VehicleId);
        maintenance.Kind.Should().Be(VehicleMaintenanceKind.Service);
        maintenance.PerformedOn.Should().Be(Performed);
        maintenance.Description.Should().Be("Ölwechsel und Filter");
        maintenance.Workshop.Should().Be("Werkstatt Nord");
        maintenance.OdometerKm.Should().Be(250_000);
        maintenance.Cost.Should().Be(849.90m);
        maintenance.NextDueOn.Should().Be(new DateOnly(2027, 11, 10));
        maintenance.ModifiedBy.Should().Be("admin");
        maintenance.ModifiedAt.Should().Be(Now);
    }

    [Fact]
    public void Create_OnlyRequiredFields_Succeeds()
    {
        VehicleMaintenance maintenance = VehicleMaintenance.Create(
            VehicleId, VehicleMaintenanceKind.Repair, Performed, "Bremsen", "  ", null, null, null, null, Now).Value;

        maintenance.Workshop.Should().BeNull();
        maintenance.OdometerKm.Should().BeNull();
        maintenance.Cost.Should().BeNull();
        maintenance.NextDueOn.Should().BeNull();
        maintenance.ModifiedBy.Should().BeNull();
    }

    [Fact]
    public void Create_NextDueSameDay_Succeeds()
    {
        VehicleMaintenance.Create(
                VehicleId, VehicleMaintenanceKind.Inspection, Performed, "HU", null, null, null, Performed, null, Now)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_ZeroOdometerAndCost_Succeeds()
    {
        VehicleMaintenance.Create(
                VehicleId, VehicleMaintenanceKind.Other, Performed, "Garantie", null, 0, 0m, null, null, Now)
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_UnknownKind_ReturnsValidationError()
    {
        VehicleMaintenance.Create(
                VehicleId, (VehicleMaintenanceKind)99, Performed, "x", null, null, null, null, null, Now)
            .Error!.Code.Should().Be("VehicleMaintenance.UnknownKind");
    }

    [Fact]
    public void Create_PerformedBefore1900_ReturnsValidationError()
    {
        VehicleMaintenance.Create(
                VehicleId, VehicleMaintenanceKind.Service, new DateOnly(1899, 12, 31), "x", null, null, null, null, null, Now)
            .Error!.Code.Should().Be("VehicleMaintenance.PerformedTooEarly");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankDescription_ReturnsValidationError(string description)
    {
        VehicleMaintenance.Create(
                VehicleId, VehicleMaintenanceKind.Service, Performed, description, null, null, null, null, null, Now)
            .Error!.Code.Should().Be("VehicleMaintenance.DescriptionRequired");
    }

    [Fact]
    public void Create_TooLongDescription_ReturnsValidationError()
    {
        VehicleMaintenance.Create(
                VehicleId, VehicleMaintenanceKind.Service, Performed,
                new string('d', VehicleMaintenance.MaxDescriptionLength + 1), null, null, null, null, null, Now)
            .Error!.Code.Should().Be("VehicleMaintenance.DescriptionTooLong");
    }

    [Fact]
    public void Create_TooLongWorkshop_ReturnsValidationError()
    {
        VehicleMaintenance.Create(
                VehicleId, VehicleMaintenanceKind.Service, Performed, "x",
                new string('w', VehicleMaintenance.MaxWorkshopLength + 1), null, null, null, null, Now)
            .Error!.Code.Should().Be("VehicleMaintenance.WorkshopTooLong");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(VehicleMaintenance.MaxOdometerKm + 1)]
    public void Create_OdometerOutOfRange_ReturnsValidationError(int odometerKm)
    {
        VehicleMaintenance.Create(
                VehicleId, VehicleMaintenanceKind.Service, Performed, "x", null, odometerKm, null, null, null, Now)
            .Error!.Code.Should().Be("VehicleMaintenance.OdometerOutOfRange");
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(10_000_000.01)]
    public void Create_CostOutOfRange_ReturnsValidationError(decimal cost)
    {
        VehicleMaintenance.Create(
                VehicleId, VehicleMaintenanceKind.Service, Performed, "x", null, null, cost, null, null, Now)
            .Error!.Code.Should().Be("VehicleMaintenance.CostOutOfRange");
    }

    [Fact]
    public void Create_NextDueBeforePerformed_ReturnsValidationError()
    {
        VehicleMaintenance.Create(
                VehicleId, VehicleMaintenanceKind.Service, Performed, "x", null, null, null,
                Performed.AddDays(-1), null, Now)
            .Error!.Code.Should().Be("VehicleMaintenance.NextDueBeforePerformed");
    }

    [Fact]
    public void Update_ValidData_ReplacesValuesAndRestamps()
    {
        VehicleMaintenance maintenance = VehicleMaintenance.Create(
            VehicleId, VehicleMaintenanceKind.Service, Performed, "Ölwechsel", "Nord", 1000, 10m,
            Performed.AddMonths(12), "alice", Now).Value;
        DateTimeOffset later = Now.AddDays(1);

        maintenance.Update(
            VehicleMaintenanceKind.Repair, Performed.AddDays(2), "Turbolader", null, null, null, null, "bob", later);

        maintenance.Kind.Should().Be(VehicleMaintenanceKind.Repair);
        maintenance.PerformedOn.Should().Be(Performed.AddDays(2));
        maintenance.Description.Should().Be("Turbolader");
        maintenance.Workshop.Should().BeNull();
        maintenance.OdometerKm.Should().BeNull();
        maintenance.Cost.Should().BeNull();
        maintenance.NextDueOn.Should().BeNull();
        maintenance.ModifiedBy.Should().Be("bob");
        maintenance.ModifiedAt.Should().Be(later);
    }

    [Fact]
    public void Update_InvalidData_LeavesMaintenanceUnchanged()
    {
        VehicleMaintenance maintenance = VehicleMaintenance.Create(
            VehicleId, VehicleMaintenanceKind.Service, Performed, "Ölwechsel", null, null, null, null, "alice", Now).Value;

        Result<VehicleMaintenance> result = maintenance.Update(
            VehicleMaintenanceKind.Repair, Performed, " ", null, null, null, null, "bob", Now.AddDays(1));

        result.IsSuccess.Should().BeFalse();
        maintenance.Kind.Should().Be(VehicleMaintenanceKind.Service);
        maintenance.Description.Should().Be("Ölwechsel");
        maintenance.ModifiedBy.Should().Be("alice");
        maintenance.ModifiedAt.Should().Be(Now);
    }

    [Fact]
    public void Update_VeryLongUserName_IsTruncated()
    {
        VehicleMaintenance maintenance = VehicleMaintenance.Create(
            VehicleId, VehicleMaintenanceKind.Service, Performed, "x", null, null, null, null,
            new string('u', 150), Now).Value;

        maintenance.ModifiedBy.Should().HaveLength(Vehicle.MaxModifiedByLength);
    }
}
