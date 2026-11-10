using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Vehicles.VehicleDocuments;

internal static class VehicleDocumentErrors
{
    public static Error VehicleNotFound(Guid vehicleId) =>
        Error.NotFound("Vehicle.NotFound", $"No vehicle with id '{vehicleId}'.");

    public static Error NotFound(Guid documentId) =>
        Error.NotFound("VehicleDocument.NotFound", $"No special document with id '{documentId}'.");

    public static Error DuplicateType(string documentType) =>
        Error.Conflict("VehicleDocument.DuplicateType", $"This vehicle already has a '{documentType}' document.");
}