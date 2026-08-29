using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Orders.UpdateOrder;

internal sealed class UpdateOrderCommandHandler(ITransportOrderRepository repository)
    : ICommandHandler<UpdateOrderCommand, OrderResponse>
{
    public async Task<Result<OrderResponse>> Handle(
        UpdateOrderCommand command,
        CancellationToken cancellationToken)
    {
        TransportOrder? order = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (order is null)
        {
            return Error.NotFound("TransportOrder.NotFound", $"No transport order with id '{command.Id}'.");
        }

        Result<Address> consignor = Address.Create(
            command.Consignor.Name,
            command.Consignor.Street,
            command.Consignor.PostalCode,
            command.Consignor.City,
            command.Consignor.Country);

        if (!consignor.IsSuccess)
        {
            return consignor.Error!;
        }

        Result<Address> consignee = Address.Create(
            command.Consignee.Name,
            command.Consignee.Street,
            command.Consignee.PostalCode,
            command.Consignee.City,
            command.Consignee.Country);

        if (!consignee.IsSuccess)
        {
            return consignee.Error!;
        }

        Result<Cargo> cargo = Cargo.Create(
            command.CargoDescription, command.CargoWeightKg, command.CargoLoadMeters);

        if (!cargo.IsSuccess)
        {
            return cargo.Error!;
        }

        Result<TimeWindow> pickup = TimeWindow.Create(command.PickupFrom, command.PickupTo);
        if (!pickup.IsSuccess)
        {
            return pickup.Error!;
        }

        Result<TimeWindow> delivery = TimeWindow.Create(command.DeliveryFrom, command.DeliveryTo);
        if (!delivery.IsSuccess)
        {
            return delivery.Error!;
        }

        // TransportOrder.Update refuses a non-draft order with a Conflict, so the handler does
        // not re-check the status: the rule lives in the entity, and duplicating it here would
        // be the same mistake a validator duplicating a domain invariant makes.
        Result<TransportOrder> updated = order.Update(
            consignor.Value, consignee.Value, cargo.Value, pickup.Value, delivery.Value);

        if (!updated.IsSuccess)
        {
            return updated.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return OrderResponse.From(updated.Value);
    }
}
