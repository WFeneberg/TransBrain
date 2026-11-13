using TransBrain.Domain.Common;

namespace TransBrain.Domain.Drivers;

/// <summary>
/// The work-clothing sizes of a driver ("Kleidergrößen"): clothing, shoe and glove size, free
/// text as in the legacy system. Every value is optional.
/// </summary>
public sealed record DriverClothingSizes
{
    public const int MaxSizeLength = 50;

    // A fresh instance per call, for the same EF Core owned-entity reason as VehicleDetails.Empty.
    public static DriverClothingSizes Empty => new();

    private DriverClothingSizes()
    {
    }

    /// <summary>Clothing size ("Konfektionsgroesse").</summary>
    public string? ClothingSize { get; private init; }

    /// <summary>Shoe size ("Schuhgroesse").</summary>
    public string? ShoeSize { get; private init; }

    /// <summary>Glove size ("Handschuhe").</summary>
    public string? GloveSize { get; private init; }

    public static Result<DriverClothingSizes> Create(
        string? clothingSize = null,
        string? shoeSize = null,
        string? gloveSize = null)
    {
        DriverClothingSizes sizes = new()
        {
            ClothingSize = OptionalText.Normalize(clothingSize),
            ShoeSize = OptionalText.Normalize(shoeSize),
            GloveSize = OptionalText.Normalize(gloveSize),
        };

        if (sizes.ClothingSize is { Length: > MaxSizeLength }
            || sizes.ShoeSize is { Length: > MaxSizeLength }
            || sizes.GloveSize is { Length: > MaxSizeLength })
        {
            return Error.Validation(
                "Driver.ClothingSizesTooLong",
                $"A clothing size must not exceed {MaxSizeLength} characters.");
        }

        return sizes;
    }
}
