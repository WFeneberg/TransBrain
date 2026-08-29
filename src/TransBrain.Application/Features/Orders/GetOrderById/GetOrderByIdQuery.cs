using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Orders.GetOrderById;

public sealed record GetOrderByIdQuery(Guid Id) : IQuery<OrderResponse>;
