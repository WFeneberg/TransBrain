using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

public sealed record DriverResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string[] LicenseClasses,
    DateOnly LicenseValidUntil,
    string Status,
    string? ExternalUserId)
{
    public DriverSocialInsuranceData SocialInsurance { get; init; } = new();

    public DriverDocumentsData Documents { get; init; } = new();

    public DriverClothingSizesData ClothingSizes { get; init; } = new();

    public IReadOnlyList<DriverRelativeData> Relatives { get; init; } = [];

    public IReadOnlyList<DriverSpecialDocumentData> SpecialDocuments { get; init; } = [];

    public IReadOnlyList<DriverPhoneNumberData> PhoneNumbers { get; init; } = [];

    public static DriverResponse From(Driver driver) => new(
        driver.Id,
        driver.FirstName,
        driver.LastName,
        // Driver.LicenseClasses is backed by a HashSet, so its iteration order depends on how the
        // entity was built (insertion order for a freshly created driver, sorted-column order for
        // one loaded from the database). The entity is free to hold a set; the wire response is
        // not, so we sort here to give callers a deterministic order regardless of the source path.
        driver.LicenseClasses.Select(c => c.ToString()).OrderBy(c => c, StringComparer.Ordinal).ToArray(),
        driver.LicenseValidUntil,
        driver.Status.ToString(),
        driver.ExternalUserId)
    {
        SocialInsurance = DriverSocialInsuranceData.From(driver.SocialInsurance),
        Documents = DriverDocumentsData.From(driver.Documents),
        ClothingSizes = DriverClothingSizesData.From(driver.ClothingSizes),
        Relatives = driver.Relatives.Select(DriverRelativeData.From).ToList(),
        SpecialDocuments = driver.SpecialDocuments.Select(DriverSpecialDocumentData.From).ToList(),
        PhoneNumbers = driver.PhoneNumbers.Select(DriverPhoneNumberData.From).ToList(),
    };
}
