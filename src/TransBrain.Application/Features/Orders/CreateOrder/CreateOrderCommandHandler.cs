using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Orders.CreateOrder;

internal sealed class CreateOrderCommandHandler(
    ITransportOrderRepository repository,
    IOrderNumberGenerator orderNumbers,
    TimeProvider timeProvider)
    : ICommandHandler<CreateOrderCommand, OrderResponse>
{
    public async Task<Result<OrderResponse>> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
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

        DateTimeOffset now = timeProvider.GetUtcNow();
        OrderNumber orderNumber = await orderNumbers.NextAsync(now.Year, cancellationToken);

        Result<TransportOrder> order = TransportOrder.Create(
            orderNumber,
            consignor.Value,
            consignee.Value,
            cargo.Value,
            pickup.Value,
            delivery.Value,
            now);

        if (!order.IsSuccess)
        {
            return order.Error!;
        }

        Result<TransportOrder> added = await repository.AddAsync(order.Value, cancellationToken);
        if (!added.IsSuccess)
        {
            return added.Error!;
        }

        return OrderResponse.From(added.Value);
    }
}
