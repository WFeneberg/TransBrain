namespace TransBrain.Application.Features.Vehicles.VehicleDocuments;

/// <summary>The fields adding and updating a special document share.</summary>
public interface IVehicleDocumentWriteCommand
{
    Guid VehicleId { get; }

    /// <summary>Code of an entry in the document type catalog.</summary>
    string DocumentType { get; }

    DateOnly? LastCheckedOn { get; }

    DateOnly? ValidUntil { get; }

    string? Remark { get; }
}