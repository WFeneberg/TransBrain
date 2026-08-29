using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.GetVehicleById;

internal sealed class GetVehicleByIdQueryHandler(IVehicleRepository repository)
    : IQueryHandler<GetVehicleByIdQuery, VehicleResponse>
{
    public async Task<Result<VehicleResponse>> Handle(
        GetVehicleByIdQuery query,
        CancellationToken cancellationToken)
    {
        Vehicle? vehicle = await repository.GetByIdAsync(query.Id, cancellationToken);

        if (vehicle is null)
        {
            return Error.NotFound("Vehicle.NotFound", $"No vehicle with id '{query.Id}'.");
        }

        return VehicleResponse.From(vehicle);
    }
}
