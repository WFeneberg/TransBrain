using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Orders.CancelOrder;

/// <remarks>
/// Returns the updated OrderResponse rather than Unit, unlike the Phase 2 delete commands:
/// cancelling changes an order the caller still cares about, so returning its new state saves a
/// round trip and lets a list refresh straight from the response.
/// </remarks>
public sealed record CancelOrderCommand(Guid Id) : ICommand<OrderResponse>;
