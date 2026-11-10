using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments.UpdateVehicleDocument;

internal sealed class UpdateVehicleDocumentCommandHandler(
    IVehicleRepository repository,
    IVehicleCatalogRepository catalog,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateVehicleDocumentCommand, VehicleDocumentResponse>
{
    public async Task<Result<VehicleDocumentResponse>> Handle(
        UpdateVehicleDocumentCommand command,
        CancellationToken cancellationToken)
    {
        VehicleDocument? document = await repository.GetDocumentAsync(
            command.VehicleId, command.DocumentId, cancellationToken);
        if (document is null)
        {
            return VehicleDocumentErrors.NotFound(command.DocumentId);
        }

        Result<string?> type = await VehicleCatalogLookup.ResolveAsync(
            catalog,
            VehicleCatalogKind.DocumentType,
            command.DocumentType,
            document.DocumentType,
            nameof(UpdateVehicleDocumentCommand.DocumentType),
            cancellationToken);
        if (!type.IsSuccess)
        {
            return type.Error!;
        }

        string documentType = type.Value ?? string.Empty;
        if (await repository.DocumentTypeExistsAsync(command.VehicleId, documentType, document.Id, cancellationToken))
        {
            return VehicleDocumentErrors.DuplicateType(documentType);
        }

        Result<VehicleDocument> updated = document.Update(
            documentType,
            command.LastCheckedOn,
            command.ValidUntil,
            command.Remark,
            currentUser.UserName,
            timeProvider.GetUtcNow());
        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return VehicleDocumentResponse.From(updated.Value);
    }
}