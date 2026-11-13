using FluentValidation;
using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

/// <remarks>
/// Mirrors <see cref="DriverClothingSizes.Create"/> so all field problems are reported at once,
/// keyed "ClothingSizes.&lt;Field&gt;" by the parent validators.
/// </remarks>
internal sealed class DriverClothingSizesDataValidator : AbstractValidator<DriverClothingSizesData>
{
    public DriverClothingSizesDataValidator()
    {
        RuleFor(s => s.ClothingSize).MaximumLength(DriverClothingSizes.MaxSizeLength);
        RuleFor(s => s.ShoeSize).MaximumLength(DriverClothingSizes.MaxSizeLength);
        RuleFor(s => s.GloveSize).MaximumLength(DriverClothingSizes.MaxSizeLength);
    }
}
