using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments.AddVehicleDocument;

internal sealed class AddVehicleDocumentCommandHandler(
    IVehicleRepository repository,
    IVehicleCatalogRepository catalog,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<AddVehicleDocumentCommand, VehicleDocumentResponse>
{
    public async Task<Result<VehicleDocumentResponse>> Handle(
        AddVehicleDocumentCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(command.VehicleId, cancellationToken) is null)
        {
            return VehicleDocumentErrors.VehicleNotFound(command.VehicleId);
        }

        Result<string?> type = await VehicleCatalogLookup.ResolveAsync(
            catalog,
            VehicleCatalogKind.DocumentType,
            command.DocumentType,
            currentCode: null,
            nameof(AddVehicleDocumentCommand.DocumentType),
            cancellationToken);
        if (!type.IsSuccess)
        {
            return type.Error!;
        }

        Result<VehicleDocument> document = VehicleDocument.Create(
            command.VehicleId,
            type.Value ?? string.Empty,
            command.LastCheckedOn,
            command.ValidUntil,
            command.Remark,
            currentUser.UserName,
            timeProvider.GetUtcNow());
        if (!document.IsSuccess)
        {
            return document.Error!;
        }

        if (await repository.DocumentTypeExistsAsync(
                command.VehicleId, document.Value.DocumentType, excludingId: null, cancellationToken))
        {
            return VehicleDocumentErrors.DuplicateType(document.Value.DocumentType);
        }

        Result<VehicleDocument> added = await repository.AddDocumentAsync(document.Value, cancellationToken);
        return added.IsSuccess ? VehicleDocumentResponse.From(added.Value) : added.Error!;
    }
}