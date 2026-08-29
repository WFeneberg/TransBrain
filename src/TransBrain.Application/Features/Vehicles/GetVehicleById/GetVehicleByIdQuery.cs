using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Vehicles.GetVehicleById;

public sealed record GetVehicleByIdQuery(Guid Id) : IQuery<VehicleResponse>;
