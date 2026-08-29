using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Tours;
using TransBrain.Application.Features.Tours.AssignOrder;
using TransBrain.Application.Features.Tours.CompleteTour;
using TransBrain.Application.Features.Tours.CreateTour;
using TransBrain.Application.Features.Tours.GetTourById;
using TransBrain.Application.Features.Tours.ListTours;
using TransBrain.Application.Features.Tours.RemoveOrder;
using TransBrain.Application.Features.Tours.StartTour;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

/// <remarks>
/// Planning is DispatchWrite (admin, disponent). Starting and completing is TourStatusWrite,
/// which additionally admits fahrer — but only for their own tours, and that half of spec §9's
/// rule cannot live in a policy: a policy sees the request, not which tour it addresses. It is
/// enforced in the handlers, via TourAccess.
/// </remarks>
public sealed class TourEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/tours").WithTags("Tours");

        group.MapPost("/", async (CreateTourCommand command, ISender sender, CancellationToken ct) =>
            {
                Result<TourResponse> result = await sender.Send(command, ct);
                return result.ToHttpResult(tour => Results.Created($"/api/tours/{tour.Id}", tour));
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("CreateTour")
            .Produces<TourResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async (
                ISender sender,
                CancellationToken ct,
                int page = 1,
                int pageSize = 20,
                DateOnly? tourDate = null,
                Guid? vehicleId = null,
                Guid? driverId = null) =>
            {
                Result<PagedResult<TourResponse>> result = await sender.Send(
                    new ListToursQuery(page, pageSize, tourDate, vehicleId, driverId), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListTours")
            .Produces<PagedResult<TourResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<TourResponse> result = await sender.Send(new GetTourByIdQuery(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetTourById")
            .Produces<TourResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/orders", async (
                Guid id, AssignOrderRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<TourResponse> result = await sender.Send(
                    new AssignOrderCommand(id, request.TransportOrderId), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("AssignOrderToTour")
            .Produces<TourResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // DELETE, unlike cancelling an order: a stop really is removed, and the order goes back
        // to Draft as if it had never been planned. Nothing is archived, so nothing is lost.
        group.MapDelete("/{id:guid}/orders/{orderId:guid}", async (
                Guid id, Guid orderId, ISender sender, CancellationToken ct) =>
            {
                Result<TourResponse> result = await sender.Send(new RemoveOrderCommand(id, orderId), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("RemoveOrderFromTour")
            .Produces<TourResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/start", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<TourResponse> result = await sender.Send(new StartTourCommand(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.TourStatusWrite)
            .WithName("StartTour")
            .Produces<TourResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/complete", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<TourResponse> result = await sender.Send(new CompleteTourCommand(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.TourStatusWrite)
            .WithName("CompleteTour")
            .Produces<TourResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}

/// <summary>Body of an order assignment. The tour id comes from the route, not the payload.</summary>
public sealed record AssignOrderRequest(Guid TransportOrderId);
