using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Orders.CreateOrder;

public sealed record CreateOrderCommand(
    AddressPayload Consignor,
    AddressPayload Consignee,
    string CargoDescription,
    int CargoWeightKg,
    decimal CargoLoadMeters,
    DateTimeOffset PickupFrom,
    DateTimeOffset PickupTo,
    DateTimeOffset DeliveryFrom,
    DateTimeOffset DeliveryTo) : ICommand<OrderResponse>;
