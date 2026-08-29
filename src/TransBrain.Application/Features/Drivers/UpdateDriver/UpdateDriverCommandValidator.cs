using FluentValidation;

namespace TransBrain.Application.Features.Drivers.UpdateDriver;

/// <remarks>
/// These rules exist to report several field problems at once, which a domain factory
/// returning a single coded error cannot do. They deliberately mirror — and never extend —
/// the domain's own checks: if these two ever disagree, the domain wins and this is the copy
/// to delete.
/// </remarks>
public sealed class UpdateDriverCommandValidator : AbstractValidator<UpdateDriverCommand>
{
    public UpdateDriverCommandValidator()
    {
        RuleFor(c => c.FirstName).NotEmpty();
        RuleFor(c => c.LastName).NotEmpty();
        RuleFor(c => c.LicenseClasses).NotEmpty();
    }
}
