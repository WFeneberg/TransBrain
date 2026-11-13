using FluentValidation;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

/// <remarks>
/// Mirrors <see cref="DriverSocialInsuranceInfo.Create"/> so all field problems are reported at
/// once, keyed "SocialInsurance.&lt;Field&gt;" by the parent validators.
/// </remarks>
internal sealed class DriverSocialInsuranceDataValidator : AbstractValidator<DriverSocialInsuranceData>
{
    public DriverSocialInsuranceDataValidator()
    {
        RuleFor(s => s.DateOfBirth)
            .Must(date => date!.Value.Year >= DriverSocialInsuranceInfo.MinYear)
            .When(s => s.DateOfBirth is not null)
            .WithMessage($"The date of birth must not be before {DriverSocialInsuranceInfo.MinYear}.");
        RuleFor(s => s.EntryDate)
            .Must(date => date!.Value.Year >= DriverSocialInsuranceInfo.MinYear)
            .When(s => s.EntryDate is not null)
            .WithMessage($"The entry date must not be before {DriverSocialInsuranceInfo.MinYear}.");
        RuleFor(s => s.PlaceOfBirth).MaximumLength(DriverSocialInsuranceInfo.MaxPlaceOfBirthLength);
        RuleFor(s => s.Nationality).MaximumLength(DriverSocialInsuranceInfo.MaxNationalityLength);
        RuleFor(s => s.SocialInsuranceNumber)
            .MaximumLength(DriverSocialInsuranceInfo.MaxSocialInsuranceNumberLength);
        RuleFor(s => s.HealthInsurance).MaximumLength(DriverSocialInsuranceInfo.MaxHealthInsuranceLength);
        RuleFor(s => s.BankName).MaximumLength(DriverSocialInsuranceInfo.MaxBankNameLength);
        RuleFor(s => s.BankCode).MaximumLength(DriverSocialInsuranceInfo.MaxBankCodeLength);
        RuleFor(s => s.AccountNumber).MaximumLength(DriverSocialInsuranceInfo.MaxAccountNumberLength);
        RuleFor(s => s.Bic).MaximumLength(DriverSocialInsuranceInfo.MaxBicLength);
        RuleFor(s => s.Iban).MaximumLength(DriverSocialInsuranceInfo.MaxIbanLength);
        RuleFor(s => s.FamilyDoctor).MaximumLength(DriverSocialInsuranceInfo.MaxFamilyDoctorLength);
        RuleFor(s => s.FamilyDoctorPhone)
            .MaximumLength(DriverSocialInsuranceInfo.MaxFamilyDoctorPhoneLength);
    }
}
