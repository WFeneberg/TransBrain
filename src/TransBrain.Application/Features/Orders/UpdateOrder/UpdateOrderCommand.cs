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
    DateTimeOffset DeliveryTo,
    Guid? ConsigneeLocationId = null,
    string? Notes = null,
    string? ConfirmationNotes = null,
    bool RequiresPartitionWall = false,
    bool RequiresDoubleDeck = false) : ICommand<OrderResponse>;
