using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments.UpdateVehicleDocument;

public sealed record UpdateVehicleDocumentCommand(
    Guid VehicleId,
    Guid DocumentId,
    string DocumentType,
    DateOnly? LastCheckedOn = null,
    DateOnly? ValidUntil = null,
    string? Remark = null) : ICommand<VehicleDocumentResponse>, IVehicleDocumentWriteCommand;