using TransBrain.Domain.Common;

namespace TransBrain.Domain.Vehicles;

/// <summary>
/// A special document or periodic inspection of a vehicle ("Sonderpapier"), for example the ADR
/// certificate or the tachograph inspection, with the date it was last checked and how long it is valid.
/// A vehicle holds each document type at most once.
/// </summary>
public sealed class VehicleDocument
{
    public const int MaxRemarkLength = 500;

    // EF Core materialization only. Every other construction goes through Create.
    private VehicleDocument()
    {
        DocumentType = null!;
    }

    private VehicleDocument(Guid id, Guid vehicleId)
    {
        Id = id;
        VehicleId = vehicleId;
        DocumentType = null!;
    }

    public Guid Id { get; private set; }

    public Guid VehicleId { get; private set; }

    /// <summary>Code of an entry in the document type catalog (<see cref="VehicleCatalogKind.DocumentType"/>).</summary>
    public string DocumentType { get; private set; }

    public DateOnly? LastCheckedOn { get; private set; }

    public DateOnly? ValidUntil { get; private set; }

    public string? Remark { get; private set; }

    public DateTimeOffset ModifiedAt { get; private set; }

    /// <summary>Login name of whoever last changed the document.</summary>
    public string? ModifiedBy { get; private set; }

    public static Result<VehicleDocument> Create(
        Guid vehicleId,
        string documentType,
        DateOnly? lastCheckedOn,
        DateOnly? validUntil,
        string? remark,
        string? modifiedBy,
        DateTimeOffset modifiedAt)
    {
        return new VehicleDocument(Guid.CreateVersion7(), vehicleId)
            .Update(documentType, lastCheckedOn, validUntil, remark, modifiedBy, modifiedAt);
    }

    public Result<VehicleDocument> Update(
        string documentType,
        DateOnly? lastCheckedOn,
        DateOnly? validUntil,
        string? remark,
        string? modifiedBy,
        DateTimeOffset modifiedAt)
    {
        string? type = OptionalText.Normalize(documentType);
        string? normalizedRemark = OptionalText.Normalize(remark);

        Error? error = Validate(type, lastCheckedOn, validUntil, normalizedRemark);
        if (error is not null)
        {
            return error;
        }

        DocumentType = type!;
        LastCheckedOn = lastCheckedOn;
        ValidUntil = validUntil;
        Remark = normalizedRemark;

        string? name = OptionalText.Normalize(modifiedBy);
        ModifiedBy = name is { Length: > Vehicle.MaxModifiedByLength } ? name[..Vehicle.MaxModifiedByLength] : name;
        ModifiedAt = modifiedAt;

        return this;
    }

    private static Error? Validate(string? type, DateOnly? lastCheckedOn, DateOnly? validUntil, string? remark)
    {
        if (type is null)
        {
            return Error.Validation("VehicleDocument.TypeRequired", "The document type must not be empty.");
        }

        if (type.Length > VehicleCatalogEntry.MaxCodeLength)
        {
            return Error.Validation(
                "VehicleDocument.TypeTooLong",
                $"The document type code must not exceed {VehicleCatalogEntry.MaxCodeLength} characters.");
        }

        if (lastCheckedOn is { Year: < VehicleDetails.MinManufactureYear }
            || validUntil is { Year: < VehicleDetails.MinManufactureYear })
        {
            return Error.Validation(
                "VehicleDocument.DateTooEarly",
                $"Dates must not be before {VehicleDetails.MinManufactureYear}.");
        }

        if (lastCheckedOn is { } checkedOn && validUntil is { } until && until < checkedOn)
        {
            return Error.Validation(
                "VehicleDocument.ValidUntilBeforeLastChecked",
                "The document must not expire before it was last checked.");
        }

        if (remark is { Length: > MaxRemarkLength })
        {
            return Error.Validation(
                "VehicleDocument.RemarkTooLong", $"The remark must not exceed {MaxRemarkLength} characters.");
        }

        return null;
    }
}
