using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Application.Features.Vehicles.CreateVehicle;
using TransBrain.Application.Features.Vehicles.ListVehicles;
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
                int pageSize = 20) =>
            {
                Result<PagedResult<VehicleResponse>> result =
                    await sender.Send(new ListVehiclesQuery(page, pageSize), cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("ListVehicles")
            .Produces<PagedResult<VehicleResponse>>()
            .ProducesValidationProblem()
            .RequireAuthorization(Policies.Read);
    }
}
