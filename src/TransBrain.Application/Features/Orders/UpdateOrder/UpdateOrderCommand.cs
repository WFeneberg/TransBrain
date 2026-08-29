using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Orders.UpdateOrder;

public sealed record UpdateOrderCommand(
    Guid Id,
    AddressPayload Consignor,
    AddressPayload Consignee,
    string CargoDescription,
    int CargoWeightKg,
    decimal CargoLoadMeters,
    DateTimeOffset PickupFrom,
    DateTimeOffset PickupTo,
    DateTimeOffset DeliveryFrom,
    DateTimeOffset DeliveryTo) : ICommand<OrderResponse>;
