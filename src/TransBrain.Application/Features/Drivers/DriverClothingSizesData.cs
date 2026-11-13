using TransBrain.Domain.Drivers;

namespace TransBrain.Application.Features.Drivers;

/// <summary>
/// Wire shape of <see cref="DriverClothingSizes"/> ("Kleidergrößen"), used in both the driver
/// write bodies and <see cref="DriverResponse"/>.
/// </summary>
public sealed record DriverClothingSizesData(
    string? ClothingSize = null,
    string? ShoeSize = null,
    string? GloveSize = null)
{
    public static DriverClothingSizesData From(DriverClothingSizes sizes) =>
        new(sizes.ClothingSize, sizes.ShoeSize, sizes.GloveSize);
}
