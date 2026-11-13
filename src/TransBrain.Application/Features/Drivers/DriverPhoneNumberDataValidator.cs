using FluentValidation;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

/// <remarks>Keyed "PhoneNumbers[i].&lt;Field&gt;" by the parent validators, so the form can mark the exact row.</remarks>
internal sealed class DriverPhoneNumberDataValidator : AbstractValidator<DriverPhoneNumberData>
{
    public DriverPhoneNumberDataValidator()
    {
        RuleFor(p => p.Number).NotEmpty().MaximumLength(DriverPhoneNumber.MaxNumberLength);
        RuleFor(p => p.Type).MaximumLength(DriverPhoneNumber.MaxTypeLength);
        RuleFor(p => p.Comment).MaximumLength(DriverPhoneNumber.MaxCommentLength);
    }
}
