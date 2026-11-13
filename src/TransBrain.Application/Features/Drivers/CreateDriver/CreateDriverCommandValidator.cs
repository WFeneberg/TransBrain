using FluentValidation;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers.CreateDriver;

/// <remarks>
/// These rules exist to report several field problems at once, which a domain factory
/// returning a single coded error cannot do. They deliberately mirror — and never extend —
/// the domain's own checks: if these two ever disagree, the domain wins and this is the copy
/// to delete.
/// </remarks>
public sealed class CreateDriverCommandValidator : AbstractValidator<CreateDriverCommand>
{
    public CreateDriverCommandValidator()
    {
        RuleFor(c => c.FirstName).NotEmpty();
        RuleFor(c => c.LastName).NotEmpty();
        RuleFor(c => c.LicenseClasses).NotEmpty();
        RuleFor(c => c.SocialInsurance!)
            .SetValidator(new DriverSocialInsuranceDataValidator())
            .When(c => c.SocialInsurance is not null);
        RuleFor(c => c.Documents!)
            .SetValidator(new DriverDocumentsDataValidator())
            .When(c => c.Documents is not null);
        RuleFor(c => c.ClothingSizes!)
            .SetValidator(new DriverClothingSizesDataValidator())
            .When(c => c.ClothingSizes is not null);
        RuleFor(c => c.Relatives!.Count)
            .LessThanOrEqualTo(Driver.MaxRelatives)
            .When(c => c.Relatives is not null)
            .OverridePropertyName(nameof(CreateDriverCommand.Relatives))
            .WithMessage($"A driver can have at most {Driver.MaxRelatives} relatives.");
        RuleForEach(c => c.Relatives).SetValidator(new DriverRelativeDataValidator());
        RuleFor(c => c.SpecialDocuments!.Count)
            .LessThanOrEqualTo(Driver.MaxSpecialDocuments)
            .When(c => c.SpecialDocuments is not null)
            .OverridePropertyName(nameof(CreateDriverCommand.SpecialDocuments))
            .WithMessage($"A driver can have at most {Driver.MaxSpecialDocuments} special papers.");
        RuleForEach(c => c.SpecialDocuments).SetValidator(new DriverSpecialDocumentDataValidator());
        RuleFor(c => c.PhoneNumbers!.Count)
            .LessThanOrEqualTo(Driver.MaxPhoneNumbers)
            .When(c => c.PhoneNumbers is not null)
            .OverridePropertyName(nameof(CreateDriverCommand.PhoneNumbers))
            .WithMessage($"A driver can have at most {Driver.MaxPhoneNumbers} phone numbers.");
        RuleForEach(c => c.PhoneNumbers).SetValidator(new DriverPhoneNumberDataValidator());
    }
}
