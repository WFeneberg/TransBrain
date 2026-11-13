using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Orders.OrderTolls.AddOrderToll;

internal sealed class AddOrderTollCommandHandler(
    ITransportOrderRepository repository,
    IVehicleCatalogRepository catalog,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<AddOrderTollCommand, OrderTollResponse>
{
    public async Task<Result<OrderTollResponse>> Handle(
        AddOrderTollCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(command.OrderId, cancellationToken) is null)
        {
            return OrderTollErrors.OrderNotFound(command.OrderId);
        }

        Result<string?> category = await VehicleCatalogLookup.ResolveAsync(
            catalog,
            VehicleCatalogKind.TollCategory,
            command.TollCategory,
            currentCode: null,
            nameof(AddOrderTollCommand.TollCategory),
            cancellationToken);
        if (!category.IsSuccess)
        {
            return category.Error!;
        }

        Result<OrderTollCharge> toll = OrderTollCharge.Create(
            command.OrderId,
            category.Value ?? string.Empty,
            command.EmptyKm,
            command.LoadedKm,
            command.FactorPerKm,
            currentUser.UserName,
            timeProvider.GetUtcNow());
        if (!toll.IsSuccess)
        {
            return toll.Error!;
        }

        await repository.AddTollAsync(toll.Value, cancellationToken);

        return OrderTollResponse.From(toll.Value);
    }
}
