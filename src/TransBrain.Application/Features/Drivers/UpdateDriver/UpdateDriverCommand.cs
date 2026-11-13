using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Drivers.UpdateDriver;

/// <remarks>The optional parts replace the driver's data as a whole; omitted means cleared.</remarks>
public sealed record UpdateDriverCommand(
    Guid Id,
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
