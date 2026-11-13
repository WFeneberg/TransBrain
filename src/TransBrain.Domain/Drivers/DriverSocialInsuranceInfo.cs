using TransBrain.Domain.Common;

namespace TransBrain.Domain.Drivers;

/// <summary>
/// The social insurance data of a driver ("Sozv-Daten"): birth data, nationality, the entry date,
/// the social insurance number, the health insurance, the bank account and the family doctor.
/// Every value is optional.
/// </summary>
public sealed record DriverSocialInsuranceInfo
{
    public const int MaxPlaceOfBirthLength = 200;
    public const int MaxNationalityLength = 100;
    public const int MaxSocialInsuranceNumberLength = 50;
    public const int MaxHealthInsuranceLength = 500;
    public const int MaxBankNameLength = 200;
    public const int MaxBankCodeLength = 20;
    public const int MaxAccountNumberLength = 34;
    public const int MaxBicLength = 11;
    public const int MaxIbanLength = 34;
    public const int MaxFamilyDoctorLength = 500;
    public const int MaxFamilyDoctorPhoneLength = 50;
    public const int MinYear = 1900;

    // A fresh instance per call, for the same EF Core owned-entity reason as VehicleDetails.Empty.
    public static DriverSocialInsuranceInfo Empty => new();

    private DriverSocialInsuranceInfo()
    {
    }

    /// <summary>Date of birth ("Geb_Dat.").</summary>
    public DateOnly? DateOfBirth { get; private init; }

    /// <summary>Place of birth ("Geb_Ort").</summary>
    public string? PlaceOfBirth { get; private init; }

    /// <summary>Nationality ("Staatsangeh."), free text as in the legacy system.</summary>
    public string? Nationality { get; private init; }

    /// <summary>Social insurance number ("Sozialvers-Nr.").</summary>
    public string? SocialInsuranceNumber { get; private init; }

    /// <summary>Date the driver joined the company ("Eintritts-Dat.").</summary>
    public DateOnly? EntryDate { get; private init; }

    /// <summary>Health insurance company, including its address ("Krankenkasse").</summary>
    public string? HealthInsurance { get; private init; }

    /// <summary>Name of the bank ("Bank").</summary>
    public string? BankName { get; private init; }

    /// <summary>National bank code ("BLZ").</summary>
    public string? BankCode { get; private init; }

    /// <summary>National account number ("Konto").</summary>
    public string? AccountNumber { get; private init; }

    /// <summary>BIC, upper case ("BIC").</summary>
    public string? Bic { get; private init; }

    /// <summary>IBAN, upper case ("IBAN").</summary>
    public string? Iban { get; private init; }

    /// <summary>Family doctor, including the practice address ("Hausarzt").</summary>
    public string? FamilyDoctor { get; private init; }

    /// <summary>Phone number of the family doctor ("Tel_Hausarzt").</summary>
    public string? FamilyDoctorPhone { get; private init; }

    public static Result<DriverSocialInsuranceInfo> Create(
        DateOnly? dateOfBirth = null,
        string? placeOfBirth = null,
        string? nationality = null,
        string? socialInsuranceNumber = null,
        DateOnly? entryDate = null,
        string? healthInsurance = null,
        string? bankName = null,
        string? bankCode = null,
        string? accountNumber = null,
        string? bic = null,
        string? iban = null,
        string? familyDoctor = null,
        string? familyDoctorPhone = null)
    {
        DriverSocialInsuranceInfo info = new()
        {
            DateOfBirth = dateOfBirth,
            PlaceOfBirth = OptionalText.Normalize(placeOfBirth),
            Nationality = OptionalText.Normalize(nationality),
            SocialInsuranceNumber = OptionalText.Normalize(socialInsuranceNumber),
            EntryDate = entryDate,
            HealthInsurance = OptionalText.Normalize(healthInsurance),
            BankName = OptionalText.Normalize(bankName),
            BankCode = OptionalText.Normalize(bankCode),
            AccountNumber = OptionalText.Normalize(accountNumber),
            // BIC and IBAN are case-insensitive identifiers; stored upper case so the same
            // account never exists in two spellings.
            Bic = OptionalText.Normalize(bic)?.ToUpperInvariant(),
            Iban = OptionalText.Normalize(iban)?.ToUpperInvariant(),
            FamilyDoctor = OptionalText.Normalize(familyDoctor),
            FamilyDoctorPhone = OptionalText.Normalize(familyDoctorPhone),
        };

        Error? error = info.Validate();
        return error is null ? info : error;
    }

    private Error? Validate()
    {
        if (DateOfBirth is { Year: < MinYear })
        {
            return Error.Validation(
                "Driver.DateOfBirthTooEarly", $"The date of birth must not be before {MinYear}.");
        }

        if (EntryDate is { Year: < MinYear })
        {
            return Error.Validation(
                "Driver.EntryDateTooEarly", $"The entry date must not be before {MinYear}.");
        }

        if (PlaceOfBirth is { Length: > MaxPlaceOfBirthLength }
            || Nationality is { Length: > MaxNationalityLength }
            || SocialInsuranceNumber is { Length: > MaxSocialInsuranceNumberLength }
            || HealthInsurance is { Length: > MaxHealthInsuranceLength })
        {
            return Error.Validation(
                "Driver.SocialInsuranceTextTooLong",
                $"Place of birth and health insurance must not exceed {MaxPlaceOfBirthLength} and "
                + $"{MaxHealthInsuranceLength} characters, nationality {MaxNationalityLength}, the "
                + $"social insurance number {MaxSocialInsuranceNumberLength}.");
        }

        if (BankName is { Length: > MaxBankNameLength }
            || BankCode is { Length: > MaxBankCodeLength }
            || AccountNumber is { Length: > MaxAccountNumberLength }
            || Bic is { Length: > MaxBicLength }
            || Iban is { Length: > MaxIbanLength })
        {
            return Error.Validation(
                "Driver.BankDetailsTooLong",
                $"The bank name must not exceed {MaxBankNameLength} characters, the bank code "
                + $"{MaxBankCodeLength}, the account number and IBAN {MaxIbanLength}, the BIC {MaxBicLength}.");
        }

        if (FamilyDoctor is { Length: > MaxFamilyDoctorLength }
            || FamilyDoctorPhone is { Length: > MaxFamilyDoctorPhoneLength })
        {
            return Error.Validation(
                "Driver.FamilyDoctorTooLong",
                $"The family doctor must not exceed {MaxFamilyDoctorLength} characters, "
                + $"the phone number {MaxFamilyDoctorPhoneLength}.");
        }

        return null;
    }
}
