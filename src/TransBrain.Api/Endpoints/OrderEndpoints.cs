using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Orders.CancelOrder;
using TransBrain.Application.Features.Orders.CreateOrder;
using TransBrain.Application.Features.Orders.GetOrderById;
using TransBrain.Application.Features.Orders.ListOrders;
using TransBrain.Application.Features.Orders.UpdateOrder;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

/// <remarks>
/// Writes use Policies.DispatchWrite (admin and disponent), NOT MasterDataWrite: orders are
/// dispatch data, and a dispatcher who cannot create an order cannot do their job.
/// </remarks>
public sealed class OrderEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/orders").WithTags("Orders");

        group.MapPost("/", async (CreateOrderCommand command, ISender sender, CancellationToken ct) =>
            {
                Result<OrderResponse> result = await sender.Send(command, ct);
                return result.ToHttpResult(order => Results.Created($"/api/orders/{order.Id}", order));
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("CreateOrder")
            .Produces<OrderResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/", async (
                ISender sender,
                CancellationToken ct,
                int page = 1,
                int pageSize = 20,
                string? status = null,
                DateTimeOffset? pickupFrom = null,
                DateTimeOffset? pickupTo = null) =>
            {
                Result<PagedResult<OrderResponse>> result = await sender.Send(
                    new ListOrdersQuery(page, pageSize, status, pickupFrom, pickupTo), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListOrders")
            .Produces<PagedResult<OrderResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<OrderResponse> result = await sender.Send(new GetOrderByIdQuery(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetOrderById")
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id, UpdateOrderRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<OrderResponse> result = await sender.Send(
                    new UpdateOrderCommand(
                        id,
                        request.Consignor,
                        request.Consignee,
                        request.CargoDescription,
                        request.CargoWeightKg,
                        request.CargoLoadMeters,
                        request.PickupFrom,
                        request.PickupTo,
                        request.DeliveryFrom,
                        request.DeliveryTo),
                    ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("UpdateOrder")
            .Produces<OrderResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Cancelling is a state transition, not a deletion: a haulier keeps the record of an
        // order that was placed and withdrawn. Hence POST to a sub-resource rather than DELETE.
        group.MapPost("/{id:guid}/cancel", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<OrderResponse> result = await sender.Send(new CancelOrderCommand(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("CancelOrder")
            .Produces<OrderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

/// <summary>Body of an order update. The id comes from the route, not the payload.</summary>
public sealed record UpdateOrderRequest(
    AddressPayload Consignor,
    AddressPayload Consignee,
    string CargoDescription,
    int CargoWeightKg,
    decimal CargoLoadMeters,
    DateTimeOffset PickupFrom,
    DateTimeOffset PickupTo,
    DateTimeOffset DeliveryFrom,
    DateTimeOffset DeliveryTo);
