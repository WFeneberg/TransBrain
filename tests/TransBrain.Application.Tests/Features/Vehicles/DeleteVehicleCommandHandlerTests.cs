using AwesomeAssertions;
using TransBrain.Application.Features.Vehicles.DeleteVehicle;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class DeleteVehicleCommandHandlerTests
{
    [Fact]
    public async Task Handle_KnownVehicle_RemovesItAndSaves()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.Van, 3_000, 4.0m, new DateOnly(2027, 3, 31)).Value;
        repository.Seed(vehicle);
        DeleteVehicleCommandHandler handler = new(repository);

        Result<Unit> result = await handler.Handle(new DeleteVehicleCommand(vehicle.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Vehicles.Should().BeEmpty();
        repository.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownVehicle_ReturnsNotFoundAndDoesNotSave()
    {
        InMemoryVehicleRepository repository = new();
        DeleteVehicleCommandHandler handler = new(repository);

        Result<Unit> result = await handler.Handle(
            new DeleteVehicleCommand(Guid.CreateVersion7()), CancellationToken.None);

        result.Error!.Type.Should().Be(ErrorType.NotFound);
        repository.SaveChangesCallCount.Should().Be(0);
    }
}
