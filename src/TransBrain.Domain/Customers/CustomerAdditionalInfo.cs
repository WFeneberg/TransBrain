using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Customers;

/// <summary>
/// The additional data of a customer ("Zusatz"): a secondary name and address, company figures,
/// billing flags, the customer class and a free comment. Every value is optional; the counts
/// default to zero and the flags to false.
/// </summary>
public sealed record CustomerAdditionalInfo
{
    public const int MaxNameLength = 200;
    public const int MaxStreetLength = 200;
    public const int MaxPostalCodeLength = 20;
    public const int MaxCityLength = 200;
    public const int CountryLength = 2;
    public const int MaxHeadcount = 1_000_000;
    public const int MinFoundingYear = 1800;
    public const int MaxCommentLength = 2000;

    // A fresh instance per call, for the same EF Core owned-entity reason as VehicleDetails.Empty.
    public static CustomerAdditionalInfo Empty => new();

    private CustomerAdditionalInfo()
    {
    }

    /// <summary>Secondary name ("Zusatz-Name"), for example a company name addition or a postal recipient.</summary>
    public string? SecondaryName { get; private init; }

    public string? SecondaryStreet { get; private init; }

    public string? SecondaryPostalCode { get; private init; }

    public string? SecondaryCity { get; private init; }

    /// <summary>ISO 3166-1 alpha-2 code of the secondary address ("Staat"), upper case.</summary>
    public string? SecondaryCountry { get; private init; }

    /// <summary>Date the company was founded ("Gründungsdatum").</summary>
    public DateOnly? FoundedOn { get; private init; }

    /// <summary>Number of blue-collar workers ("Anzahl Arbeiter").</summary>
    public int WorkerCount { get; private init; }

    /// <summary>Number of salaried employees ("Anzahl Angestellte").</summary>
    public int SalariedEmployeeCount { get; private init; }

    /// <summary>Pallets of this customer are checked on exchange ("Palettenprüfung").</summary>
    public bool RequiresPalletCheck { get; private init; }

    /// <summary>Toll is not charged on this customer's invoices ("Maut nicht in Rechnung (Kunde)").</summary>
    public bool ExcludeTollFromInvoice { get; private init; }

    /// <summary>Toll is reimbursed when the customer works as subcontractor ("Maut erstatten (Subunternehmer)").</summary>
    public bool ReimburseToll { get; private init; }

    /// <summary>Code of the customer class ("Kundenklasse") from the customer class catalog.</summary>
    public string? CustomerClass { get; private init; }

    /// <summary>Free comment ("Kommentar").</summary>
    public string? Comment { get; private init; }

    public static Result<CustomerAdditionalInfo> Create(
        string? secondaryName = null,
        string? secondaryStreet = null,
        string? secondaryPostalCode = null,
        string? secondaryCity = null,
        string? secondaryCountry = null,
        DateOnly? foundedOn = null,
        int workerCount = 0,
        int salariedEmployeeCount = 0,
        bool requiresPalletCheck = false,
        bool excludeTollFromInvoice = false,
        bool reimburseToll = false,
        string? customerClass = null,
        string? comment = null)
    {
        string? country = OptionalText.Normalize(secondaryCountry);

        // Shape checked before upper-casing, for the reason given in Address.Create.
        if (country is not null && (country.Length != CountryLength || !country.All(char.IsAsciiLetter)))
        {
            return Error.Validation(
                "Customer.SecondaryCountryInvalid",
                "Country must be an ISO 3166-1 alpha-2 code, for example 'DE'.");
        }

        CustomerAdditionalInfo info = new()
        {
            SecondaryName = OptionalText.Normalize(secondaryName),
            SecondaryStreet = OptionalText.Normalize(secondaryStreet),
            SecondaryPostalCode = OptionalText.Normalize(secondaryPostalCode),
            SecondaryCity = OptionalText.Normalize(secondaryCity),
            SecondaryCountry = country?.ToUpperInvariant(),
            FoundedOn = foundedOn,
            WorkerCount = workerCount,
            SalariedEmployeeCount = salariedEmployeeCount,
            RequiresPalletCheck = requiresPalletCheck,
            ExcludeTollFromInvoice = excludeTollFromInvoice,
            ReimburseToll = reimburseToll,
            CustomerClass = OptionalText.Normalize(customerClass),
            Comment = OptionalText.Normalize(comment),
        };

        Error? error = info.Validate();
        return error is null ? info : error;
    }

    private Error? Validate()
    {
        if (SecondaryName is { Length: > MaxNameLength }
            || SecondaryStreet is { Length: > MaxStreetLength }
            || SecondaryPostalCode is { Length: > MaxPostalCodeLength }
            || SecondaryCity is { Length: > MaxCityLength })
        {
            return Error.Validation(
                "Customer.SecondaryAddressTooLong",
                $"Secondary name, street and city must not exceed {MaxNameLength} characters, the postal code {MaxPostalCodeLength}.");
        }

        if (FoundedOn is { Year: < MinFoundingYear })
        {
            return Error.Validation(
                "Customer.FoundedOnTooEarly", $"The founding date must not be before {MinFoundingYear}.");
        }

        if (WorkerCount is < 0 or > MaxHeadcount || SalariedEmployeeCount is < 0 or > MaxHeadcount)
        {
            return Error.Validation(
                "Customer.HeadcountOutOfRange",
                $"The number of workers and salaried employees must be between 0 and {MaxHeadcount}.");
        }

        if (CustomerClass is { Length: > VehicleCatalogEntry.MaxCodeLength })
        {
            return Error.Validation(
                "Customer.CustomerClassTooLong",
                $"A customer class code must not exceed {VehicleCatalogEntry.MaxCodeLength} characters.");
        }

        if (Comment is { Length: > MaxCommentLength })
        {
            return Error.Validation(
                "Customer.CommentTooLong", $"The comment must not exceed {MaxCommentLength} characters.");
        }

        return null;
    }
}
