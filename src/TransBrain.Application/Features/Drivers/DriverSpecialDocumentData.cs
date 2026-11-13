using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Drivers;

/// <summary>Wire shape of <see cref="DriverSpecialDocument"/> ("Sonderpapiere"), used in driver write bodies and responses.</summary>
public sealed record DriverSpecialDocumentData(
    string Type,
    DateOnly? LastInspectionDate = null,
    DateOnly? ValidUntil = null,
    string? Comment = null)
{
    public static DriverSpecialDocumentData From(DriverSpecialDocument document) => new(
        document.Type, document.LastInspectionDate, document.ValidUntil, document.Comment);

    /// <summary>
    /// Turns the wire special papers into domain ones. A missing list means "none". Each type is
    /// resolved against the driver-document-type catalog; a type the driver already has stays accepted
    /// even when its entry was deactivated in the meantime. Other field problems are already caught by
    /// the command validators, so a failure from the domain itself is only its own backstop.
    /// </summary>
    public static async Task<Result<List<DriverSpecialDocument>>> ToDomainAsync(
        IReadOnlyList<DriverSpecialDocumentData>? documents,
        IVehicleCatalogRepository catalog,
        IReadOnlyCollection<string> currentTypeCodes,
        CancellationToken cancellationToken)
    {
        List<DriverSpecialDocument> result = [with(capacity: documents?.Count ?? 0)];
        int index = 0;
        foreach (DriverSpecialDocumentData data in documents ?? [])
        {
            string? submitted = OptionalText.Normalize(data.Type);
            string? currentCode = currentTypeCodes.FirstOrDefault(
                code => string.Equals(code, submitted, StringComparison.OrdinalIgnoreCase));
            Result<string?> type = await VehicleCatalogLookup.ResolveAsync(
                catalog, VehicleCatalogKind.DriverDocumentType, data.Type, currentCode,
                $"SpecialDocuments[{index}].Type", cancellationToken);
            if (!type.IsSuccess)
            {
                return type.Error!;
            }

            Result<DriverSpecialDocument> document = DriverSpecialDocument.Create(
                type.Value!, data.LastInspectionDate, data.ValidUntil, data.Comment);
            if (!document.IsSuccess)
            {
                return document.Error!;
            }

            result.Add(document.Value);
            index++;
        }

        return result;
    }
}
