using TransBrain.Application.Abstractions;
using TransBrain.Domain.Common;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

/// <summary>Turns the wire values of a driver write into domain values.</summary>
internal static class DriverWriteMapping
{
    /// <summary>Everything a driver write carries besides the top-level fields.</summary>
    internal sealed record DriverWriteValues(
        DriverSocialInsuranceInfo SocialInsurance,
        DriverDocumentsInfo Documents,
        DriverClothingSizes ClothingSizes,
        List<DriverRelative> Relatives,
        List<DriverSpecialDocument> SpecialDocuments,
        List<DriverPhoneNumber> PhoneNumbers);

    /// <summary>
    /// Maps all optional driver write parts in one go; the first failure wins. The phone and special
    /// paper types are resolved against the catalog; <paramref name="currentPhoneTypeCodes"/> and
    /// <paramref name="currentDocumentTypeCodes"/> are the codes the stored driver already uses (empty
    /// on create), which stay accepted even when deactivated.
    /// </summary>
    public static async Task<Result<DriverWriteValues>> ToDomainAsync(
        DriverSocialInsuranceData? socialInsurance,
        DriverDocumentsData? documents,
        DriverClothingSizesData? clothingSizes,
        IReadOnlyList<DriverRelativeData>? relatives,
        IReadOnlyList<DriverSpecialDocumentData>? specialDocuments,
        IReadOnlyList<DriverPhoneNumberData>? phoneNumbers,
        IVehicleCatalogRepository catalog,
        IReadOnlyCollection<string> currentPhoneTypeCodes,
        IReadOnlyCollection<string> currentDocumentTypeCodes,
        CancellationToken cancellationToken)
    {
        Result<DriverSocialInsuranceInfo> social = ToSocialInsurance(socialInsurance);
        if (!social.IsSuccess)
        {
            return social.Error!;
        }

        Result<DriverDocumentsInfo> documentsInfo = ToDocuments(documents);
        if (!documentsInfo.IsSuccess)
        {
            return documentsInfo.Error!;
        }

        Result<DriverClothingSizes> sizes = ToClothingSizes(clothingSizes);
        if (!sizes.IsSuccess)
        {
            return sizes.Error!;
        }

        Result<List<DriverRelative>> relativesList = DriverRelativeData.ToDomain(relatives);
        if (!relativesList.IsSuccess)
        {
            return relativesList.Error!;
        }

        Result<List<DriverSpecialDocument>> documentsList = await DriverSpecialDocumentData.ToDomainAsync(
            specialDocuments, catalog, currentDocumentTypeCodes, cancellationToken);
        if (!documentsList.IsSuccess)
        {
            return documentsList.Error!;
        }

        Result<List<DriverPhoneNumber>> phonesList = await DriverPhoneNumberData.ToDomainAsync(
            phoneNumbers, catalog, currentPhoneTypeCodes, cancellationToken);
        if (!phonesList.IsSuccess)
        {
            return phonesList.Error!;
        }

        return new DriverWriteValues(
            social.Value, documentsInfo.Value, sizes.Value,
            relativesList.Value, documentsList.Value, phonesList.Value);
    }

    /// <remarks>An omitted body clears the social insurance data, like on create.</remarks>
    public static Result<DriverSocialInsuranceInfo> ToSocialInsurance(DriverSocialInsuranceData? data)
    {
        if (data is null)
        {
            return DriverSocialInsuranceInfo.Empty;
        }

        return DriverSocialInsuranceInfo.Create(
            data.DateOfBirth,
            data.PlaceOfBirth,
            data.Nationality,
            data.SocialInsuranceNumber,
            data.EntryDate,
            data.HealthInsurance,
            data.BankName,
            data.BankCode,
            data.AccountNumber,
            data.Bic,
            data.Iban,
            data.FamilyDoctor,
            data.FamilyDoctorPhone);
    }

    /// <remarks>An omitted body clears the licence document data, like on create.</remarks>
    public static Result<DriverDocumentsInfo> ToDocuments(DriverDocumentsData? data)
    {
        if (data is null)
        {
            return DriverDocumentsInfo.Empty;
        }

        return DriverDocumentsInfo.Create(data.LicenseNumber, data.IssuingAuthority, data.IssueDate);
    }

    /// <remarks>An omitted body clears the clothing sizes, like on create.</remarks>
    public static Result<DriverClothingSizes> ToClothingSizes(DriverClothingSizesData? data)
    {
        if (data is null)
        {
            return DriverClothingSizes.Empty;
        }

        return DriverClothingSizes.Create(data.ClothingSize, data.ShoeSize, data.GloveSize);
    }
}
