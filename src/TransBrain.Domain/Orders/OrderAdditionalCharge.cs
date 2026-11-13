using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Domain.Orders;

/// <summary>
/// One additional charge position of a transport order ("Zusatzposition"), for example waiting
/// hours or an unloading flat fee: a description, a quantity in a unit from the unit catalog and
/// a unit price. The total is derived, never stored: quantity times unit price, rounded to cents.
/// </summary>
public sealed class OrderAdditionalCharge
{
    public const int MaxDescriptionLength = 200;
    public const decimal MaxQuantity = 1_000_000m;
    public const decimal MaxUnitPrice = 1_000_000m;

    // EF Core materialization only. Every other construction goes through Create.
    private OrderAdditionalCharge()
    {
        Description = null!;
    }

    private OrderAdditionalCharge(Guid id, Guid orderId)
    {
        Id = id;
        OrderId = orderId;
        Description = null!;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    /// <summary>What is charged ("Bezeichnung").</summary>
    public string Description { get; private set; }

    /// <summary>How much of the unit is charged ("Menge").</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Code of an entry in the unit catalog (<see cref="VehicleCatalogKind.Unit"/>), if any.</summary>
    public string? Unit { get; private set; }

    /// <summary>Price per unit ("Einzelpreis").</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Kaufmännisch rounded to cents, matching the old software's "Gesamt" column.</summary>
    public decimal Total => Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero);

    public DateTimeOffset ModifiedAt { get; private set; }

    /// <summary>Login name of whoever last changed the position.</summary>
    public string? ModifiedBy { get; private set; }

    public static Result<OrderAdditionalCharge> Create(
        Guid orderId,
        string description,
        decimal quantity,
        string? unit,
        decimal unitPrice,
        string? modifiedBy,
        DateTimeOffset modifiedAt)
    {
        return new OrderAdditionalCharge(Guid.CreateVersion7(), orderId)
            .Update(description, quantity, unit, unitPrice, modifiedBy, modifiedAt);
    }

    public Result<OrderAdditionalCharge> Update(
        string description,
        decimal quantity,
        string? unit,
        decimal unitPrice,
        string? modifiedBy,
        DateTimeOffset modifiedAt)
    {
        string? normalizedDescription = OptionalText.Normalize(description);
        string? normalizedUnit = OptionalText.Normalize(unit);

        Error? error = Validate(normalizedDescription, quantity, normalizedUnit, unitPrice);
        if (error is not null)
        {
            return error;
        }

        Description = normalizedDescription!;
        Quantity = quantity;
        Unit = normalizedUnit;
        UnitPrice = unitPrice;

        string? name = OptionalText.Normalize(modifiedBy);
        ModifiedBy = name is { Length: > Vehicle.MaxModifiedByLength } ? name[..Vehicle.MaxModifiedByLength] : name;
        ModifiedAt = modifiedAt;

        return this;
    }

    private static Error? Validate(string? description, decimal quantity, string? unit, decimal unitPrice)
    {
        if (description is null)
        {
            return Error.Validation(
                "OrderAdditionalCharge.DescriptionRequired", "The description must not be empty.");
        }

        if (description.Length > MaxDescriptionLength)
        {
            return Error.Validation(
                "OrderAdditionalCharge.DescriptionTooLong",
                $"The description must not exceed {MaxDescriptionLength} characters.");
        }

        if (quantity is <= 0m or > MaxQuantity)
        {
            return Error.Validation(
                "OrderAdditionalCharge.QuantityOutOfRange",
                $"The quantity must be greater than 0 and at most {MaxQuantity}.");
        }

        if (decimal.Round(quantity, 2) != quantity)
        {
            return Error.Validation(
                "OrderAdditionalCharge.QuantityTooPrecise",
                "The quantity must not have more than two decimal places.");
        }

        if (unit is { Length: > VehicleCatalogEntry.MaxCodeLength })
        {
            return Error.Validation(
                "OrderAdditionalCharge.UnitTooLong",
                $"The unit code must not exceed {VehicleCatalogEntry.MaxCodeLength} characters.");
        }

        if (unitPrice is < 0m or > MaxUnitPrice)
        {
            return Error.Validation(
                "OrderAdditionalCharge.UnitPriceOutOfRange",
                $"The unit price must be between 0 and {MaxUnitPrice}.");
        }

        if (decimal.Round(unitPrice, 2) != unitPrice)
        {
            return Error.Validation(
                "OrderAdditionalCharge.UnitPriceTooPrecise",
                "The unit price must not have more than two decimal places.");
        }

        return null;
    }
}
