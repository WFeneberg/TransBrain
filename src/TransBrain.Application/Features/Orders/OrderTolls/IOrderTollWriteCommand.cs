namespace TransBrain.Application.Features.Orders.OrderTolls;

/// <summary>The fields adding and updating a toll record share.</summary>
public interface IOrderTollWriteCommand
{
    Guid OrderId { get; }

    /// <summary>Code of an entry in the toll category catalog.</summary>
    string TollCategory { get; }

    int EmptyKm { get; }

    int LoadedKm { get; }

    decimal FactorPerKm { get; }
}
