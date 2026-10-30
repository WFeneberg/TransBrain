using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Orders.GetOrderLocations;

public sealed record GetOrderLocationsQuery(Guid Id) : IQuery<OrderLocationsResponse>;
