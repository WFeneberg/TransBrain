namespace TransBrain.Application.Features.Orders.OrderCharges;

/// <summary>The fields adding and updating an additional charge share.</summary>
public interface IOrderChargeWriteCommand
{
    Guid OrderId { get; }

    string Description { get; }

    decimal Quantity { get; }

    /// <summary>Code of an entry in the unit catalog, if any.</summary>
    string? Unit { get; }

    decimal UnitPrice { get; }
}
