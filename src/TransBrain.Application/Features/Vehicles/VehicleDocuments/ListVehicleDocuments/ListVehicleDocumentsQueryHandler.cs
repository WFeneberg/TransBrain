using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments.ListVehicleDocuments;

internal sealed class ListVehicleDocumentsQueryHandler(IVehicleRepository repository)
    : IQueryHandler<ListVehicleDocumentsQuery, VehicleDocumentResponse[]>
{
    public async Task<Result<VehicleDocumentResponse[]>> Handle(
        ListVehicleDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(query.VehicleId, cancellationToken) is null)
        {
            return VehicleDocumentErrors.VehicleNotFound(query.VehicleId);
        }

        IReadOnlyList<VehicleDocument> documents = await repository.ListDocumentsAsync(query.VehicleId, cancellationToken);

        return documents.Select(VehicleDocumentResponse.From).ToArray();
    }
}