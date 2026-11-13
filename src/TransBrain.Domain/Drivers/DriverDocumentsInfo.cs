using TransBrain.Domain.Common;

namespace TransBrain.Domain.Drivers;

/// <summary>
/// The driving licence document data of a driver ("Dokumente"): the licence number, the issuing
/// authority and the issue date. The licence classes themselves live directly on the driver.
/// Every value is optional.
/// </summary>
public sealed record DriverDocumentsInfo
{
    public const int MaxLicenseNumberLength = 50;
    public const int MaxIssuingAuthorityLength = 500;
    public const int MinYear = 1900;

    // A fresh instance per call, for the same EF Core owned-entity reason as VehicleDetails.Empty.
    public static DriverDocumentsInfo Empty => new();

    private DriverDocumentsInfo()
    {
    }

    /// <summary>Driving licence number ("Führerscheinnr.").</summary>
    public string? LicenseNumber { get; private init; }

    /// <summary>Issuing authority, including its address ("Ausstellungsbeh.").</summary>
    public string? IssuingAuthority { get; private init; }

    /// <summary>Date the licence was issued ("Ausstellungsdat.").</summary>
    public DateOnly? IssueDate { get; private init; }

    public static Result<DriverDocumentsInfo> Create(
        string? licenseNumber = null,
        string? issuingAuthority = null,
        DateOnly? issueDate = null)
    {
        DriverDocumentsInfo info = new()
        {
            LicenseNumber = OptionalText.Normalize(licenseNumber),
            IssuingAuthority = OptionalText.Normalize(issuingAuthority),
            IssueDate = issueDate,
        };

        Error? error = info.Validate();
        return error is null ? info : error;
    }

    private Error? Validate()
    {
        if (IssueDate is { Year: < MinYear })
        {
            return Error.Validation(
                "Driver.LicenseIssueDateTooEarly", $"The licence issue date must not be before {MinYear}.");
        }

        if (LicenseNumber is { Length: > MaxLicenseNumberLength }
            || IssuingAuthority is { Length: > MaxIssuingAuthorityLength })
        {
            return Error.Validation(
                "Driver.DocumentsTextTooLong",
                $"The licence number must not exceed {MaxLicenseNumberLength} characters, "
                + $"the issuing authority {MaxIssuingAuthorityLength}.");
        }

        return null;
    }
}
