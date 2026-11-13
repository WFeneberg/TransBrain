using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Orders.OrderTolls.UpdateOrderToll;

internal sealed class UpdateOrderTollCommandHandler(
    ITransportOrderRepository repository,
    IVehicleCatalogRepository catalog,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateOrderTollCommand, OrderTollResponse>
{
    public async Task<Result<OrderTollResponse>> Handle(
        UpdateOrderTollCommand command,
        CancellationToken cancellationToken)
    {
        OrderTollCharge? toll = await repository.GetTollAsync(command.OrderId, command.TollId, cancellationToken);
        if (toll is null)
        {
            return OrderTollErrors.NotFound(command.TollId);
        }

        Result<string?> category = await VehicleCatalogLookup.ResolveAsync(
            catalog,
            VehicleCatalogKind.TollCategory,
            command.TollCategory,
            toll.TollCategory,
            nameof(UpdateOrderTollCommand.TollCategory),
            cancellationToken);
        if (!category.IsSuccess)
        {
            return category.Error!;
        }

        Result<OrderTollCharge> updated = toll.Update(
            category.Value ?? string.Empty,
            command.EmptyKm,
            command.LoadedKm,
            command.FactorPerKm,
            currentUser.UserName,
            timeProvider.GetUtcNow());
        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return OrderTollResponse.From(updated.Value);
    }
}
