using AwesomeAssertions;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.GetVehicleById;
using TransBrain.Application.Tests.Fakes;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Tests.Features.Vehicles;

public class GetVehicleByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_KnownId_ReturnsVehicle()
    {
        InMemoryVehicleRepository repository = new();
        Vehicle vehicle = Vehicle.Create(
            LicensePlate.Create("M-AB 1234").Value, VehicleType.Tractor, 24_000, 13.6m, new DateOnly(2027, 3, 31)).Value;
        repository.Seed(vehicle);
        GetVehicleByIdQueryHandler handler = new(repository);

        Result<VehicleResponse> result = await handler.Handle(
            new GetVehicleByIdQuery(vehicle.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(vehicle.Id);
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsNotFound()
    {
        GetVehicleByIdQueryHandler handler = new(new InMemoryVehicleRepository());

        Result<VehicleResponse> result = await handler.Handle(
            new GetVehicleByIdQuery(Guid.CreateVersion7()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        result.Error.Code.Should().Be("Vehicle.NotFound");
    }
}
