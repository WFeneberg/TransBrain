using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.DeleteVehicle;

internal sealed class DeleteVehicleCommandHandler(IVehicleRepository repository)
    : ICommandHandler<DeleteVehicleCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteVehicleCommand command, CancellationToken cancellationToken)
    {
        Vehicle? vehicle = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (vehicle is null)
        {
            return Error.NotFound("Vehicle.NotFound", $"No vehicle with id '{command.Id}'.");
        }

        await repository.RemoveAsync(vehicle, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
