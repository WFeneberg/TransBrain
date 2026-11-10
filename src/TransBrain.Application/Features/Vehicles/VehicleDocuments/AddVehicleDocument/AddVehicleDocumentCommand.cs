using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments.AddVehicleDocument;

public sealed record AddVehicleDocumentCommand(
    Guid VehicleId,
    string DocumentType,
    DateOnly? LastCheckedOn = null,
    DateOnly? ValidUntil = null,
    string? Remark = null) : ICommand<VehicleDocumentResponse>, IVehicleDocumentWriteCommand;