using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments;

public sealed record VehicleDocumentResponse(
    Guid Id,
    Guid VehicleId,
    string DocumentType,
    DateOnly? LastCheckedOn,
    DateOnly? ValidUntil,
    string? Remark,
    DateTimeOffset ModifiedAt,
    string? ModifiedBy)
{
    public static VehicleDocumentResponse From(VehicleDocument document) => new(
        document.Id,
        document.VehicleId,
        document.DocumentType,
        document.LastCheckedOn,
        document.ValidUntil,
        document.Remark,
        document.ModifiedAt,
        document.ModifiedBy);
}