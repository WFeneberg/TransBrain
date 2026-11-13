using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Application.Features.Orders.OrderCharges.AddOrderCharge;

internal sealed class AddOrderChargeCommandHandler(
    ITransportOrderRepository repository,
    IVehicleCatalogRepository catalog,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<AddOrderChargeCommand, OrderChargeResponse>
{
    public async Task<Result<OrderChargeResponse>> Handle(
        AddOrderChargeCommand command,
        CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(command.OrderId, cancellationToken) is null)
        {
            return OrderChargeErrors.OrderNotFound(command.OrderId);
        }

        Result<string?> unit = await VehicleCatalogLookup.ResolveAsync(
            catalog,
            VehicleCatalogKind.Unit,
            command.Unit,
            currentCode: null,
            nameof(AddOrderChargeCommand.Unit),
            cancellationToken);
        if (!unit.IsSuccess)
        {
            return unit.Error!;
        }

        Result<OrderAdditionalCharge> charge = OrderAdditionalCharge.Create(
            command.OrderId,
            command.Description,
            command.Quantity,
            unit.Value,
            command.UnitPrice,
            currentUser.UserName,
            timeProvider.GetUtcNow());
        if (!charge.IsSuccess)
        {
            return charge.Error!;
        }

        await repository.AddChargeAsync(charge.Value, cancellationToken);

        return OrderChargeResponse.From(charge.Value);
    }
}
