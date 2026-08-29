using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Drivers.CreateDriver;
using TransBrain.Application.Features.Drivers.DeleteDriver;
using TransBrain.Application.Features.Drivers.GetDriverById;
using TransBrain.Application.Features.Drivers.ListDrivers;
using TransBrain.Application.Features.Drivers.UpdateDriver;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

public sealed class DriverEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/drivers").WithTags("Drivers");

        group.MapPost("/", async (CreateDriverCommand command, ISender sender, CancellationToken ct) =>
            {
                Result<DriverResponse> result = await sender.Send(command, ct);
                return result.ToHttpResult(driver => Results.Created($"/api/drivers/{driver.Id}", driver));
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("CreateDriver")
            .Produces<DriverResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/", async (
                ISender sender, CancellationToken ct, int page = 1, int pageSize = 20, string? status = null) =>
            {
                Result<PagedResult<DriverResponse>> result =
                    await sender.Send(new ListDriversQuery(page, pageSize, status), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListDrivers")
            .Produces<PagedResult<DriverResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<DriverResponse> result = await sender.Send(new GetDriverByIdQuery(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetDriverById")
            .Produces<DriverResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id, UpdateDriverRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<DriverResponse> result = await sender.Send(
                    new UpdateDriverCommand(
                        id,
                        request.FirstName,
                        request.LastName,
                        request.LicenseClasses,
                        request.LicenseValidUntil,
                        request.ExternalUserId),
                    ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("UpdateDriver")
            .Produces<DriverResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<Unit> result = await sender.Send(new DeleteDriverCommand(id), ct);
                return result.ToHttpResult(_ => Results.NoContent());
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("DeleteDriver")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

/// <summary>Body of a driver update. The id comes from the route, not the payload.</summary>
public sealed record UpdateDriverRequest(
    string FirstName,
    string LastName,
    string[] LicenseClasses,
    DateOnly LicenseValidUntil,
    string? ExternalUserId);
