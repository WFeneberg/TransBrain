using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Orders;

/// <summary>
/// One toll record of a transport order ("Mautdaten"): a toll category with the empty and loaded
/// kilometres driven under it and the per-kilometre factor valid for the trip. The amounts are
/// derived, never stored: factor times kilometres, rounded to cents.
/// </summary>
public sealed class OrderTollCharge
{
    public const int MaxKilometers = 100_000;
    public const decimal MaxFactorPerKm = 1000m;

    // EF Core materialization only. Every other construction goes through Create.
    private OrderTollCharge()
    {
        TollCategory = null!;
    }

    private OrderTollCharge(Guid id, Guid orderId)
    {
        Id = id;
        OrderId = orderId;
        TollCategory = null!;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    /// <summary>Code of an entry in the toll category catalog (<see cref="VehicleCatalogKind.TollCategory"/>).</summary>
    public string TollCategory { get; private set; }

    /// <summary>Kilometres driven without load ("Leer-km").</summary>
    public int EmptyKm { get; private set; }

    /// <summary>Kilometres driven loaded ("Last-km").</summary>
    public int LoadedKm { get; private set; }

    /// <summary>Toll per kilometre ("Faktor pro km"), copied from the tariff valid for the trip.</summary>
    public decimal FactorPerKm { get; private set; }

    /// <summary>Kaufmännisch rounded to cents, matching the old software's "Leer-Betrag" column.</summary>
    public decimal EmptyAmount => Math.Round(EmptyKm * FactorPerKm, 2, MidpointRounding.AwayFromZero);

    /// <summary>Kaufmännisch rounded to cents, matching the old software's "Last-Betrag" column.</summary>
    public decimal LoadedAmount => Math.Round(LoadedKm * FactorPerKm, 2, MidpointRounding.AwayFromZero);

    public DateTimeOffset ModifiedAt { get; private set; }

    /// <summary>Login name of whoever last changed the record.</summary>
    public string? ModifiedBy { get; private set; }

    public static Result<OrderTollCharge> Create(
        Guid orderId,
        string tollCategory,
        int emptyKm,
        int loadedKm,
        decimal factorPerKm,
        string? modifiedBy,
        DateTimeOffset modifiedAt)
    {
        return new OrderTollCharge(Guid.CreateVersion7(), orderId)
            .Update(tollCategory, emptyKm, loadedKm, factorPerKm, modifiedBy, modifiedAt);
    }

    public Result<OrderTollCharge> Update(
        string tollCategory,
        int emptyKm,
        int loadedKm,
        decimal factorPerKm,
        string? modifiedBy,
        DateTimeOffset modifiedAt)
    {
        string? category = OptionalText.Normalize(tollCategory);

        Error? error = Validate(category, emptyKm, loadedKm, factorPerKm);
        if (error is not null)
        {
            return error;
        }

        TollCategory = category!;
        EmptyKm = emptyKm;
        LoadedKm = loadedKm;
        FactorPerKm = factorPerKm;

        string? name = OptionalText.Normalize(modifiedBy);
        ModifiedBy = name is { Length: > Vehicle.MaxModifiedByLength } ? name[..Vehicle.MaxModifiedByLength] : name;
        ModifiedAt = modifiedAt;

        return this;
    }

    private static Error? Validate(string? category, int emptyKm, int loadedKm, decimal factorPerKm)
    {
        if (category is null)
        {
            return Error.Validation("OrderTollCharge.CategoryRequired", "The toll category must not be empty.");
        }

        if (category.Length > VehicleCatalogEntry.MaxCodeLength)
        {
            return Error.Validation(
                "OrderTollCharge.CategoryTooLong",
                $"The toll category code must not exceed {VehicleCatalogEntry.MaxCodeLength} characters.");
        }

        if (emptyKm is < 0 or > MaxKilometers || loadedKm is < 0 or > MaxKilometers)
        {
            return Error.Validation(
                "OrderTollCharge.KilometersOutOfRange",
                $"Kilometres must be between 0 and {MaxKilometers}.");
        }

        if (factorPerKm is < 0m or > MaxFactorPerKm)
        {
            return Error.Validation(
                "OrderTollCharge.FactorOutOfRange",
                $"The factor per kilometre must be between 0 and {MaxFactorPerKm}.");
        }

        if (decimal.Round(factorPerKm, 4) != factorPerKm)
        {
            return Error.Validation(
                "OrderTollCharge.FactorTooPrecise",
                "The factor per kilometre must not have more than four decimal places.");
        }

        return null;
    }
}
