using TransBrain.Domain.Common;

namespace TransBrain.Domain.Vehicles;

public sealed record LicensePlate
{
    private const int MaxLength = 15;

    private LicensePlate(string value) => Value = value;

    public string Value { get; }

    public static Result<LicensePlate> Create(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Error.Validation("LicensePlate.Empty", "License plate must not be empty.");
        }

        string normalized = input.Trim().ToUpperInvariant();

        if (normalized.Length > MaxLength)
        {
            return Error.Validation("LicensePlate.TooLong", $"License plate must not exceed {MaxLength} characters.");
        }

        return new LicensePlate(normalized);
    }

    public override string ToString() => Value;
}
