using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Drivers.CreateDriver;

public sealed record CreateDriverCommand(
    string FirstName,
    string LastName,
    string[] LicenseClasses,
    DateOnly LicenseValidUntil,
    string? ExternalUserId,
    DriverSocialInsuranceData? SocialInsurance = null,
    DriverDocumentsData? Documents = null,
    DriverClothingSizesData? ClothingSizes = null,
    IReadOnlyList<DriverRelativeData>? Relatives = null,
    IReadOnlyList<DriverSpecialDocumentData>? SpecialDocuments = null,
    IReadOnlyList<DriverPhoneNumberData>? PhoneNumbers = null) : ICommand<DriverResponse>;
