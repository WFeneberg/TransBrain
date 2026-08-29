using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Application.Features.Vehicles.DeleteVehicle;
using TransBrain.Application.Features.Vehicles.GetVehicleById;
using TransBrain.Application.Features.Vehicles.ListVehicles;
using TransBrain.Application.Features.Vehicles.UpdateVehicle;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

public sealed class VehicleEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/vehicles").WithTags("Vehicles");

        group.MapPost("/", async (
                CreateVehicleCommand command,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                Result<VehicleResponse> result = await sender.Send(command, cancellationToken);
                return result.ToHttpResult(vehicle => Results.Created($"/api/vehicles/{vehicle.Id}", vehicle));
            })
            .WithName("CreateVehicle")
            .Produces<VehicleResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .RequireAuthorization(Policies.MasterDataWrite);

        group.MapGet("/", async (
                ISender sender,
                CancellationToken cancellationToken,
                int page = 1,
                int pageSize = 20,
                string? status = null,
                string? type = null) =>
            {
                Result<PagedResult<VehicleResponse>> result =
                    await sender.Send(new ListVehiclesQuery(page, pageSize, status, type), cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("ListVehicles")
            .Produces<PagedResult<VehicleResponse>>()
            .ProducesValidationProblem()
            .RequireAuthorization(Policies.Read);

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<VehicleResponse> result = await sender.Send(new GetVehicleByIdQuery(id), cancellationToken);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetVehicleById")
            .Produces<VehicleResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id, UpdateVehicleRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<VehicleResponse> result = await sender.Send(
                    new UpdateVehicleCommand(
                        id,
                        request.LicensePlate,
                        request.Type,
                        request.PayloadKg,
                        request.LoadMeters,
                        request.NextInspectionDue),
                    cancellationToken);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("UpdateVehicle")
            .Produces<VehicleResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<Unit> result = await sender.Send(new DeleteVehicleCommand(id), cancellationToken);
                return result.ToHttpResult(_ => Results.NoContent());
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("DeleteVehicle")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

/// <summary>Body of a vehicle update. The id comes from the route, not the payload.</summary>
public sealed record UpdateVehicleRequest(
    string LicensePlate,
    string Type,
    int PayloadKg,
    decimal LoadMeters,
    DateOnly NextInspectionDue);
