using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Orders;

namespace TransBrain.Application.Features.Invoices.ListBillableOrders;

/// <summary>
/// The delivered orders that are not yet on any non-cancelled invoice — what the billing screen
/// offers for a new invoice.
/// </summary>
public sealed record ListBillableOrdersQuery : IQuery<IReadOnlyList<OrderResponse>>;
