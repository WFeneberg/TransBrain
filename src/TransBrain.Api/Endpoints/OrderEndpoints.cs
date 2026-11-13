using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Orders;
using TransBrain.Application.Features.Orders.CancelOrder;
using TransBrain.Application.Features.Orders.CreateOrder;
using TransBrain.Application.Features.Orders.GetOrderById;
using TransBrain.Application.Features.Orders.GetOrderLocations;
using TransBrain.Application.Features.Orders.ListOrders;
using TransBrain.Application.Features.Orders.OrderCharges;
using TransBrain.Application.Features.Orders.OrderCharges.AddOrderCharge;
using TransBrain.Application.Features.Orders.OrderCharges.DeleteOrderCharge;
using TransBrain.Application.Features.Orders.OrderCharges.ListOrderCharges;
using TransBrain.Application.Features.Orders.OrderCharges.UpdateOrderCharge;
using TransBrain.Application.Features.Orders.OrderTolls;
using TransBrain.Application.Features.Orders.OrderTolls.AddOrderToll;
using TransBrain.Application.Features.Orders.OrderTolls.DeleteOrderToll;
using TransBrain.Application.Features.Orders.OrderTolls.ListOrderTolls;
using TransBrain.Application.Features.Orders.OrderTolls.UpdateOrderToll;
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

        group.MapGet("/{id:guid}/locations", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<OrderLocationsResponse> result = await sender.Send(new GetOrderLocationsQuery(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetOrderLocations")
            .Produces<OrderLocationsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

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
                        request.DeliveryTo,
                        request.ConsigneeLocationId,
                        request.Notes,
                        request.ConfirmationNotes,
                        request.RequiresPartitionWall,
                        request.RequiresDoubleDeck),
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

        MapTolls(group);
        MapCharges(group);
    }

    private static void MapTolls(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/tolls", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<OrderTollResponse[]> result = await sender.Send(new ListOrderTollsQuery(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListOrderTolls")
            .WithSummary("Lists the order's toll records (\"Mautdaten\").")
            .Produces<OrderTollResponse[]>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/tolls", async (
                Guid id, OrderTollRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<OrderTollResponse> result = await sender.Send(
                    new AddOrderTollCommand(
                        id, request.TollCategory, request.EmptyKm, request.LoadedKm, request.FactorPerKm),
                    ct);
                return result.ToHttpResult(toll =>
                    Results.Created($"/api/orders/{id}/tolls/{toll.Id}", toll));
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("AddOrderToll")
            .Produces<OrderTollResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/tolls/{tollId:guid}", async (
                Guid id, Guid tollId, OrderTollRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<OrderTollResponse> result = await sender.Send(
                    new UpdateOrderTollCommand(
                        id, tollId, request.TollCategory, request.EmptyKm, request.LoadedKm, request.FactorPerKm),
                    ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("UpdateOrderToll")
            .Produces<OrderTollResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/tolls/{tollId:guid}", async (
                Guid id, Guid tollId, ISender sender, CancellationToken ct) =>
            {
                Result<Unit> result = await sender.Send(new DeleteOrderTollCommand(id, tollId), ct);
                return result.ToHttpResult(_ => Results.NoContent());
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("DeleteOrderToll")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapCharges(RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/charges", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<OrderChargeResponse[]> result = await sender.Send(new ListOrderChargesQuery(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListOrderCharges")
            .WithSummary("Lists the order's additional charge positions (\"Zusatzpositionen\").")
            .Produces<OrderChargeResponse[]>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/charges", async (
                Guid id, OrderChargeRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<OrderChargeResponse> result = await sender.Send(
                    new AddOrderChargeCommand(
                        id, request.Description, request.Quantity, request.Unit, request.UnitPrice),
                    ct);
                return result.ToHttpResult(charge =>
                    Results.Created($"/api/orders/{id}/charges/{charge.Id}", charge));
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("AddOrderCharge")
            .Produces<OrderChargeResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}/charges/{chargeId:guid}", async (
                Guid id, Guid chargeId, OrderChargeRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<OrderChargeResponse> result = await sender.Send(
                    new UpdateOrderChargeCommand(
                        id, chargeId, request.Description, request.Quantity, request.Unit, request.UnitPrice),
                    ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("UpdateOrderCharge")
            .Produces<OrderChargeResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/charges/{chargeId:guid}", async (
                Guid id, Guid chargeId, ISender sender, CancellationToken ct) =>
            {
                Result<Unit> result = await sender.Send(new DeleteOrderChargeCommand(id, chargeId), ct);
                return result.ToHttpResult(_ => Results.NoContent());
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("DeleteOrderCharge")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
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
    DateTimeOffset DeliveryTo,
    Guid? ConsigneeLocationId = null,
    string? Notes = null,
    string? ConfirmationNotes = null,
    bool RequiresPartitionWall = false,
    bool RequiresDoubleDeck = false);

/// <summary>Body of a toll record write. Order and record ids come from the route.</summary>
public sealed record OrderTollRequest(
    string TollCategory,
    int EmptyKm = 0,
    int LoadedKm = 0,
    decimal FactorPerKm = 0m);

/// <summary>Body of an additional charge write. Order and charge ids come from the route.</summary>
public sealed record OrderChargeRequest(
    string Description,
    decimal Quantity,
    string? Unit = null,
    decimal UnitPrice = 0m);
