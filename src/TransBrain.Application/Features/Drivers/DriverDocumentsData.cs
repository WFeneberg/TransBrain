using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

/// <summary>
/// Wire shape of <see cref="DriverDocumentsInfo"/> ("Dokumente"), used in both the driver write
/// bodies and <see cref="DriverResponse"/>.
/// </summary>
public sealed record DriverDocumentsData(
    string? LicenseNumber = null,
    string? IssuingAuthority = null,
    DateOnly? IssueDate = null)
{
    public static DriverDocumentsData From(DriverDocumentsInfo info) =>
        new(info.LicenseNumber, info.IssuingAuthority, info.IssueDate);
}
