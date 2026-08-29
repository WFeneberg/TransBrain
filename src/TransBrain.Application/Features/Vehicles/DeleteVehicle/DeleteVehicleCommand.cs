using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Vehicles.DeleteVehicle;

public sealed record DeleteVehicleCommand(Guid Id) : ICommand<Unit>;
