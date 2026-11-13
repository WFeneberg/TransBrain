using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

/// <summary>
/// Wire shape of <see cref="DriverSocialInsuranceInfo"/> ("Sozv-Daten"), used in both the driver
/// write bodies and <see cref="DriverResponse"/>.
/// </summary>
public sealed record DriverSocialInsuranceData(
    DateOnly? DateOfBirth = null,
    string? PlaceOfBirth = null,
    string? Nationality = null,
    string? SocialInsuranceNumber = null,
    DateOnly? EntryDate = null,
    string? HealthInsurance = null,
    string? BankName = null,
    string? BankCode = null,
    string? AccountNumber = null,
    string? Bic = null,
    string? Iban = null,
    string? FamilyDoctor = null,
    string? FamilyDoctorPhone = null)
{
    public static DriverSocialInsuranceData From(DriverSocialInsuranceInfo info) => new(
        info.DateOfBirth,
        info.PlaceOfBirth,
        info.Nationality,
        info.SocialInsuranceNumber,
        info.EntryDate,
        info.HealthInsurance,
        info.BankName,
        info.BankCode,
        info.AccountNumber,
        info.Bic,
        info.Iban,
        info.FamilyDoctor,
        info.FamilyDoctorPhone);
}
