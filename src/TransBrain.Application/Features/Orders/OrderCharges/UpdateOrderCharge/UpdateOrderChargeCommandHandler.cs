using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Orders.OrderCharges.UpdateOrderCharge;

internal sealed class UpdateOrderChargeCommandHandler(
    ITransportOrderRepository repository,
    IVehicleCatalogRepository catalog,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateOrderChargeCommand, OrderChargeResponse>
{
    public async Task<Result<OrderChargeResponse>> Handle(
        UpdateOrderChargeCommand command,
        CancellationToken cancellationToken)
    {
        OrderAdditionalCharge? charge = await repository.GetChargeAsync(
            command.OrderId, command.ChargeId, cancellationToken);
        if (charge is null)
        {
            return OrderChargeErrors.NotFound(command.ChargeId);
        }

        Result<string?> unit = await VehicleCatalogLookup.ResolveAsync(
            catalog,
            VehicleCatalogKind.Unit,
            command.Unit,
            charge.Unit,
            nameof(UpdateOrderChargeCommand.Unit),
            cancellationToken);
        if (!unit.IsSuccess)
        {
            return unit.Error!;
        }

        Result<OrderAdditionalCharge> updated = charge.Update(
            command.Description,
            command.Quantity,
            unit.Value,
            command.UnitPrice,
            currentUser.UserName,
            timeProvider.GetUtcNow());
        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return OrderChargeResponse.From(updated.Value);
    }
}
