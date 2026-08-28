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
}
