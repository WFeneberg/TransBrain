using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments.DeleteVehicleDocument;

internal sealed class DeleteVehicleDocumentCommandHandler(IVehicleRepository repository)
    : ICommandHandler<DeleteVehicleDocumentCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteVehicleDocumentCommand command, CancellationToken cancellationToken)
    {
        VehicleDocument? document = await repository.GetDocumentAsync(
            command.VehicleId, command.DocumentId, cancellationToken);
        if (document is null)
        {
            return VehicleDocumentErrors.NotFound(command.DocumentId);
        }

        await repository.RemoveDocumentAsync(document, cancellationToken);

        return Unit.Value;
    }
}