using System.Collections.ObjectModel;
using TransBrain.Domain.Common;

namespace TransBrain.Domain.Drivers;

public sealed class Driver
{
    public const int MaxRelatives = 20;
    public const int MaxSpecialDocuments = 20;
    public const int MaxPhoneNumbers = 20;

    private readonly HashSet<LicenseClass> _licenseClasses = [];
    private readonly ReadOnlySet<LicenseClass> _licenseClassesView;
    private readonly List<DriverRelative> _relatives = [];
    private readonly List<DriverSpecialDocument> _specialDocuments = [];
    private readonly List<DriverPhoneNumber> _phoneNumbers = [];

    // EF Core materialization only. Every other construction goes through Create.
    private Driver()
    {
        FirstName = null!;
        LastName = null!;
        SocialInsurance = null!;
        Documents = null!;
        ClothingSizes = null!;
        _licenseClassesView = new ReadOnlySet<LicenseClass>(_licenseClasses);
    }

    private Driver(
        Guid id,
        string firstName,
        string lastName,
        IEnumerable<LicenseClass> licenseClasses,
        DateOnly licenseValidUntil,
        string? externalUserId,
        DriverSocialInsuranceInfo socialInsurance,
        DriverDocumentsInfo documents,
        DriverClothingSizes clothingSizes)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        _licenseClasses.UnionWith(licenseClasses);
        _licenseClassesView = new ReadOnlySet<LicenseClass>(_licenseClasses);
        LicenseValidUntil = licenseValidUntil;
        ExternalUserId = externalUserId;
        SocialInsurance = socialInsurance;
        Documents = documents;
        ClothingSizes = clothingSizes;
        Status = DriverStatus.Available;
    }

    public Guid Id { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public IReadOnlyCollection<LicenseClass> LicenseClasses => _licenseClassesView;

    public DateOnly LicenseValidUntil { get; private set; }

    public DriverStatus Status { get; private set; }

    /// <summary>Keycloak's <c>sub</c> claim, set when the driver has a login.</summary>
    public string? ExternalUserId { get; private set; }

    /// <summary>The social insurance data ("Sozv-Daten"); <see cref="DriverSocialInsuranceInfo.Empty"/> when nothing is set.</summary>
    public DriverSocialInsuranceInfo SocialInsurance { get; private set; }

    /// <summary>The licence document data ("Dokumente"); <see cref="DriverDocumentsInfo.Empty"/> when nothing is set.</summary>
    public DriverDocumentsInfo Documents { get; private set; }

    /// <summary>The work-clothing sizes ("Kleidergrößen"); <see cref="DriverClothingSizes.Empty"/> when nothing is set.</summary>
    public DriverClothingSizes ClothingSizes { get; private set; }

    /// <summary>The relatives ("Verwandte") in the order they were entered.</summary>
    public IReadOnlyList<DriverRelative> Relatives => _relatives.OrderBy(r => r.Sequence).ToList();

    /// <summary>The special papers ("Sonderpapiere") in the order they were entered.</summary>
    public IReadOnlyList<DriverSpecialDocument> SpecialDocuments =>
        _specialDocuments.OrderBy(d => d.Sequence).ToList();

    /// <summary>The phone numbers ("Telefone") in the order they were entered.</summary>
    public IReadOnlyList<DriverPhoneNumber> PhoneNumbers => _phoneNumbers.OrderBy(p => p.Sequence).ToList();

    public static Result<Driver> Create(
        string firstName,
        string lastName,
        IReadOnlyCollection<LicenseClass> licenseClasses,
        DateOnly licenseValidUntil,
        string? externalUserId,
        DriverSocialInsuranceInfo? socialInsurance = null,
        DriverDocumentsInfo? documents = null,
        DriverClothingSizes? clothingSizes = null,
        IReadOnlyList<DriverRelative>? relatives = null,
        IReadOnlyList<DriverSpecialDocument>? specialDocuments = null,
        IReadOnlyList<DriverPhoneNumber>? phoneNumbers = null)
    {
        Result<Unit> validation = Validate(
            firstName, lastName, licenseClasses, relatives, specialDocuments, phoneNumbers);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        var driver = new Driver(
            Guid.CreateVersion7(),
            firstName.Trim(),
            lastName.Trim(),
            licenseClasses,
            licenseValidUntil,
            NormalizeExternalUserId(externalUserId),
            socialInsurance ?? DriverSocialInsuranceInfo.Empty,
            documents ?? DriverDocumentsInfo.Empty,
            clothingSizes ?? DriverClothingSizes.Empty);
        driver.ReplaceCollections(relatives, specialDocuments, phoneNumbers);
        return driver;
    }

    public Result<Driver> Update(
        string firstName,
        string lastName,
        IReadOnlyCollection<LicenseClass> licenseClasses,
        DateOnly licenseValidUntil,
        string? externalUserId,
        DriverSocialInsuranceInfo? socialInsurance = null,
        DriverDocumentsInfo? documents = null,
        DriverClothingSizes? clothingSizes = null,
        IReadOnlyList<DriverRelative>? relatives = null,
        IReadOnlyList<DriverSpecialDocument>? specialDocuments = null,
        IReadOnlyList<DriverPhoneNumber>? phoneNumbers = null)
    {
        Result<Unit> validation = Validate(
            firstName, lastName, licenseClasses, relatives, specialDocuments, phoneNumbers);
        if (!validation.IsSuccess)
        {
            return validation.Error!;
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        _licenseClasses.Clear();
        foreach (LicenseClass licenseClass in licenseClasses)
        {
            _licenseClasses.Add(licenseClass);
        }

        LicenseValidUntil = licenseValidUntil;
        ExternalUserId = NormalizeExternalUserId(externalUserId);
        SocialInsurance = socialInsurance ?? DriverSocialInsuranceInfo.Empty;
        Documents = documents ?? DriverDocumentsInfo.Empty;
        ClothingSizes = clothingSizes ?? DriverClothingSizes.Empty;
        ReplaceCollections(relatives, specialDocuments, phoneNumbers);

        return this;
    }

    /// <remarks>
    /// Deliberately acts only on an available driver: leaving an already-absent or a
    /// deactivated driver untouched is intentional, not a forgotten branch — mirrors
    /// <see cref="MarkAvailable"/>.
    /// </remarks>
    public void MarkAbsent()
    {
        if (Status == DriverStatus.Available)
        {
            Status = DriverStatus.Absent;
        }
    }

    /// <remarks>
    /// Deliberately refuses to revive an inactive driver: deactivation is an administrative
    /// decision, and an availability toggle must not silently undo it.
    /// </remarks>
    public void MarkAvailable()
    {
        if (Status == DriverStatus.Absent)
        {
            Status = DriverStatus.Available;
        }
    }

    public void Deactivate() => Status = DriverStatus.Inactive;

    public bool CanDriveOn(DateOnly date) =>
        Status == DriverStatus.Available && LicenseValidUntil >= date;

    private void ReplaceCollections(
        IReadOnlyList<DriverRelative>? relatives,
        IReadOnlyList<DriverSpecialDocument>? specialDocuments,
        IReadOnlyList<DriverPhoneNumber>? phoneNumbers)
    {
        _relatives.Clear();
        _relatives.AddRange((relatives ?? []).Select((r, index) => r.WithSequence(index + 1)));
        _specialDocuments.Clear();
        _specialDocuments.AddRange((specialDocuments ?? []).Select((d, index) => d.WithSequence(index + 1)));
        _phoneNumbers.Clear();
        _phoneNumbers.AddRange((phoneNumbers ?? []).Select((p, index) => p.WithSequence(index + 1)));
    }

    private static Result<Unit> Validate(
        string firstName,
        string lastName,
        IReadOnlyCollection<LicenseClass> licenseClasses,
        IReadOnlyList<DriverRelative>? relatives,
        IReadOnlyList<DriverSpecialDocument>? specialDocuments,
        IReadOnlyList<DriverPhoneNumber>? phoneNumbers)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return Error.Validation("Driver.FirstNameRequired", "First name must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            return Error.Validation("Driver.LastNameRequired", "Last name must not be empty.");
        }

        if (licenseClasses.Count == 0)
        {
            return Error.Validation("Driver.LicenseClassRequired", "At least one licence class is required.");
        }

        if (relatives is { Count: > MaxRelatives })
        {
            return Error.Validation(
                "Driver.TooManyRelatives", $"A driver can have at most {MaxRelatives} relatives.");
        }

        if (specialDocuments is { Count: > MaxSpecialDocuments })
        {
            return Error.Validation(
                "Driver.TooManySpecialDocuments",
                $"A driver can have at most {MaxSpecialDocuments} special papers.");
        }

        if (phoneNumbers is { Count: > MaxPhoneNumbers })
        {
            return Error.Validation(
                "Driver.TooManyPhoneNumbers",
                $"A driver can have at most {MaxPhoneNumbers} phone numbers.");
        }

        return Unit.Value;
    }

    private static string? NormalizeExternalUserId(string? externalUserId) =>
        string.IsNullOrWhiteSpace(externalUserId) ? null : externalUserId.Trim();

    // EF Core maps this shadow-ish projection rather than the collection itself: the licence
    // classes are a small fixed set, so a comma-separated column is cheaper than a child table.
    // Sorted on write so an unchanged driver never produces a spurious UPDATE.
    private string LicenseClassesRaw
    {
        get => string.Join(',', _licenseClasses.Select(c => c.ToString()).OrderBy(c => c, StringComparer.Ordinal));
        set
        {
            _licenseClasses.Clear();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            foreach (string part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                // A token that fails to parse (a hand-edited row, or a retired enum member) is
                // deliberately dropped rather than logged or thrown: this runs during EF
                // materialization, where logging is out of place and failing the load entirely
                // would leave the driver unusable for something less severe than a missing class.
                if (Enum.TryParse(part, out LicenseClass parsed) && Enum.IsDefined(parsed))
                {
                    _licenseClasses.Add(parsed);
                }
            }
        }
    }
}
