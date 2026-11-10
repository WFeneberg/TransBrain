using AwesomeAssertions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Tests.Vehicles;

public class VehicleTests
{
    private static readonly LicensePlate Plate = LicensePlate.Create("M-AB 1234").Value;
    private static readonly DateOnly Inspection = new(2027, 3, 31);

    [Fact]
    public void Create_ValidArguments_ReturnsAvailableVehicleWithIdentity()
    {
        Result<Vehicle> result = Vehicle.Create(Plate, VehicleType.Tractor, 24_000, 13.6m, Inspection);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.LicensePlate.Should().Be(Plate);
        result.Value.Type.Should().Be(VehicleType.Tractor);
        result.Value.PayloadKg.Should().Be(24_000);
        result.Value.LoadMeters.Should().Be(13.6m);
        result.Value.NextInspectionDue.Should().Be(Inspection);
        result.Value.Status.Should().Be(VehicleStatus.Available);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonPositivePayload_ReturnsValidationError(int payloadKg)
    {
        Result<Vehicle> result = Vehicle.Create(Plate, VehicleType.Van, payloadKg, 4.0m, Inspection);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.PayloadKgNotPositive");
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Create_NonPositiveLoadMeters_ReturnsValidationError()
    {
        Result<Vehicle> result = Vehicle.Create(Plate, VehicleType.Van, 3_000, 0m, Inspection);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.LoadMetersNotPositive");
    }

    [Fact]
    public void Create_TwoVehicles_AssignsDistinctIdentities()
    {
        Vehicle first = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;
        Vehicle second = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Update_ValidArguments_ReplacesFields()
    {
        Vehicle vehicle = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;
        LicensePlate newPlate = LicensePlate.Create("M-ZZ 9999").Value;

        Result<Vehicle> result = vehicle.Update(
            newPlate, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2029, 1, 1), VehicleDetails.Empty, null);

        result.IsSuccess.Should().BeTrue();
        vehicle.LicensePlate.Should().Be(newPlate);
        vehicle.Type.Should().Be(VehicleType.Tractor);
        vehicle.PayloadKg.Should().Be(24_000);
    }

    [Fact]
    public void Update_NonPositivePayload_LeavesVehicleUnchanged()
    {
        Vehicle vehicle = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;

        Result<Vehicle> result = vehicle.Update(Plate, VehicleType.Van, 0, 4.0m, Inspection, VehicleDetails.Empty, null);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Vehicle.PayloadKgNotPositive");
        vehicle.PayloadKg.Should().Be(3_000);
    }

    [Fact]
    public void SendToWorkshop_AvailableVehicle_ChangesStatus()
    {
        Vehicle vehicle = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;

        vehicle.SendToWorkshop();

        vehicle.Status.Should().Be(VehicleStatus.InWorkshop);
    }

    [Fact]
    public void ReturnToService_DecommissionedVehicle_StaysDecommissioned()
    {
        Vehicle vehicle = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;
        vehicle.Decommission();

        vehicle.ReturnToService();

        vehicle.Status.Should().Be(VehicleStatus.Decommissioned);
    }

    [Fact]
    public void Create_WithoutDetails_UsesEmptyDetailsAndNoDriver()
    {
        Vehicle vehicle = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;

        vehicle.Details.Should().Be(VehicleDetails.Empty);
        vehicle.DefaultDriverId.Should().BeNull();
        vehicle.ModifiedAt.Should().BeNull();
    }

    [Fact]
    public void Update_NonPositivePayload_KeepsPreviousDetails()
    {
        VehicleDetails original = VehicleDetails.Create(make: "MAN").Value;
        Vehicle vehicle = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection, original).Value;

        vehicle.Update(Plate, VehicleType.Van, 0, 4.0m, Inspection, VehicleDetails.Create(make: "DAF").Value, Guid.NewGuid());

        vehicle.Details.Should().Be(original);
        vehicle.DefaultDriverId.Should().BeNull();
    }

    [Theory]
    [InlineData(VehicleStatus.Available, VehicleStatus.InWorkshop)]
    [InlineData(VehicleStatus.Available, VehicleStatus.Decommissioned)]
    [InlineData(VehicleStatus.InWorkshop, VehicleStatus.Available)]
    [InlineData(VehicleStatus.InWorkshop, VehicleStatus.Decommissioned)]
    [InlineData(VehicleStatus.Decommissioned, VehicleStatus.Decommissioned)]
    public void ChangeStatus_AllowedTransition_SetsTargetStatus(VehicleStatus from, VehicleStatus to)
    {
        Vehicle vehicle = VehicleIn(from);

        Result<Vehicle> result = vehicle.ChangeStatus(to);

        result.IsSuccess.Should().BeTrue();
        vehicle.Status.Should().Be(to);
    }

    [Theory]
    [InlineData(VehicleStatus.Available)]
    [InlineData(VehicleStatus.InWorkshop)]
    public void ChangeStatus_FromDecommissioned_ReturnsConflictAndStaysDecommissioned(VehicleStatus target)
    {
        Vehicle vehicle = VehicleIn(VehicleStatus.Decommissioned);

        Result<Vehicle> result = vehicle.ChangeStatus(target);

        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Code.Should().Be("Vehicle.Decommissioned");
        vehicle.Status.Should().Be(VehicleStatus.Decommissioned);
    }

    [Fact]
    public void ChangeStatus_UndefinedStatus_ReturnsValidationError()
    {
        Vehicle vehicle = VehicleIn(VehicleStatus.Available);

        Result<Vehicle> result = vehicle.ChangeStatus((VehicleStatus)99);

        result.Error!.Code.Should().Be("Vehicle.UnknownStatus");
        vehicle.Status.Should().Be(VehicleStatus.Available);
    }

    [Fact]
    public void RecordModification_LongName_TrimsAndTruncates()
    {
        Vehicle vehicle = VehicleIn(VehicleStatus.Available);
        DateTimeOffset at = new(2026, 11, 9, 8, 0, 0, TimeSpan.Zero);

        vehicle.RecordModification("  " + new string('x', 150) + "  ", at);

        vehicle.ModifiedBy.Should().HaveLength(Vehicle.MaxModifiedByLength);
        vehicle.ModifiedAt.Should().Be(at);
    }

    [Fact]
    public void RecordModification_BlankName_StoresNull()
    {
        Vehicle vehicle = VehicleIn(VehicleStatus.Available);

        vehicle.RecordModification("   ", DateTimeOffset.UnixEpoch);

        vehicle.ModifiedBy.Should().BeNull();
    }

    private static Vehicle VehicleIn(VehicleStatus status)
    {
        Vehicle vehicle = Vehicle.Create(Plate, VehicleType.Van, 3_000, 4.0m, Inspection).Value;
        if (status == VehicleStatus.InWorkshop)
        {
            vehicle.SendToWorkshop();
        }
        else if (status == VehicleStatus.Decommissioned)
        {
            vehicle.Decommission();
        }

        return vehicle;
    }
}
