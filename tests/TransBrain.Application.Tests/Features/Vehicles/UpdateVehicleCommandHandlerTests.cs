using AwesomeAssertions;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.UpdateVehicle;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class UpdateVehicleCommandHandlerTests
{
    private static Vehicle ExistingVehicle(string plate = "M-AB 1234") =>
        Vehicle.Create(LicensePlate.Create(plate).Value, VehicleType.Van, 3_000, 4.0m, new DateOnly(2027, 3, 31)).Value;

    [Fact]
    public async Task Handle_KnownVehicle_UpdatesFieldsAndSavesOnce()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = ExistingVehicle();
        repository.Seed(vehicle);
        UpdateVehicleCommandHandler handler = new(repository);

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
        UpdateVehicleCommandHandler handler = new(repository);

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
        UpdateVehicleCommandHandler handler = new(repository);

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
        UpdateVehicleCommandHandler handler = new(repository);

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
        UpdateVehicleCommandHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(
            new UpdateVehicleCommand(vehicle.Id, "M-AB 1234", "Tractor", 24_000, 13.6m, new DateOnly(2029, 1, 1)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.LicensePlate.Should().Be("M-AB 1234");
        repository.SaveChangesCallCount.Should().Be(1);
    }
}
