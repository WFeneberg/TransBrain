namespace TransBrain.Domain.Common;

public sealed record Cargo
{
    private Cargo(string description, int weightKg, decimal loadMeters)
    {
        Description = description;
        WeightKg = weightKg;
        LoadMeters = loadMeters;
    }

    public string Description { get; }

    public int WeightKg { get; }

    public decimal LoadMeters { get; }

    public static Result<Cargo> Create(string? description, int weightKg, decimal loadMeters)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return Error.Validation("Cargo.DescriptionRequired", "Cargo description must not be empty.");
        }

        if (weightKg <= 0)
        {
            return Error.Validation("Cargo.WeightKgNotPositive", "Cargo weight must be greater than zero.");
        }

        if (loadMeters <= 0m)
        {
            return Error.Validation("Cargo.LoadMetersNotPositive", "Load meters must be greater than zero.");
        }

        return new Cargo(description.Trim(), weightKg, loadMeters);
    }
}
