using TransBrain.Domain.Common;
using TransBrain.Domain.Orders;

namespace TransBrain.Application.Features.Orders;

public sealed record AddressPayload(
    string Name,
    string Street,
    string PostalCode,
    string City,
    string Country)
{
    public static AddressPayload From(Address address) =>
        new(address.Name, address.Street, address.PostalCode, address.City, address.Country);
}

public sealed record OrderResponse(
    Guid Id,
    string OrderNumber,
    AddressPayload Consignor,
    AddressPayload Consignee,
    string CargoDescription,
    int CargoWeightKg,
    decimal CargoLoadMeters,
    DateTimeOffset PickupFrom,
    DateTimeOffset PickupTo,
    DateTimeOffset DeliveryFrom,
    DateTimeOffset DeliveryTo,
    string Status,
    DateTimeOffset CreatedAt)
{
    public static OrderResponse From(TransportOrder order) => new(
        order.Id,
        order.OrderNumber.Value,
        AddressPayload.From(order.Consignor),
        AddressPayload.From(order.Consignee),
        order.Cargo.Description,
        order.Cargo.WeightKg,
        order.Cargo.LoadMeters,
        order.PickupWindow.From,
        order.PickupWindow.To,
        order.DeliveryWindow.From,
        order.DeliveryWindow.To,
        order.Status.ToString(),
        order.CreatedAt);
}
