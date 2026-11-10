using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.VehicleCatalog;
using TransBrain.Application.Features.VehicleCatalog.CreateVehicleCatalogEntry;
using TransBrain.Application.Features.VehicleCatalog.DeleteVehicleCatalogEntry;
using TransBrain.Application.Features.VehicleCatalog.ListVehicleCatalog;
using TransBrain.Application.Features.VehicleCatalog.UpdateVehicleCatalogEntry;
using TransBrain.Domain.Common;
using TransBrain.Domain.Vehicles;

namespace TransBrain.Api.Endpoints;

/// <summary>
/// The maintainable vehicle selection lists ("Fahrzeug-Stammdaten"). <c>{kind}</c> is
/// <c>VehicleClass</c> or <c>CarrierGroup</c>, case-insensitive.
/// </summary>
public sealed class VehicleCatalogEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/vehicle-catalog/{kind}").WithTags("Vehicle catalog");

        group.MapGet("/", async (string kind, ISender sender, CancellationToken ct) =>
            {
                if (!TryParseKind(kind, out VehicleCatalogKind parsed))
                {
                    return UnknownKind(kind);
                }

                Result<IReadOnlyList<VehicleCatalogEntryResponse>> result =
                    await sender.Send(new ListVehicleCatalogQuery(parsed), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListVehicleCatalog")
            .Produces<IReadOnlyList<VehicleCatalogEntryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/", async (
                string kind, CreateVehicleCatalogEntryRequest request, ISender sender, CancellationToken ct) =>
            {
                if (!TryParseKind(kind, out VehicleCatalogKind parsed))
                {
                    return UnknownKind(kind);
                }

                Result<VehicleCatalogEntryResponse> result = await sender.Send(
                    new CreateVehicleCatalogEntryCommand(parsed, request.Code, request.Name, request.IsActive), ct);
                return result.ToHttpResult(entry =>
                    Results.Created($"/api/vehicle-catalog/{entry.Kind}/{entry.Id}", entry));
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("CreateVehicleCatalogEntry")
            .Produces<VehicleCatalogEntryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (
                string kind, Guid id, UpdateVehicleCatalogEntryRequest request, ISender sender, CancellationToken ct) =>
            {
                if (!TryParseKind(kind, out VehicleCatalogKind parsed))
                {
                    return UnknownKind(kind);
                }

                Result<VehicleCatalogEntryResponse> result = await sender.Send(
                    new UpdateVehicleCatalogEntryCommand(parsed, id, request.Name, request.IsActive), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("UpdateVehicleCatalogEntry")
            .Produces<VehicleCatalogEntryResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (string kind, Guid id, ISender sender, CancellationToken ct) =>
            {
                if (!TryParseKind(kind, out VehicleCatalogKind parsed))
                {
                    return UnknownKind(kind);
                }

                Result<Unit> result = await sender.Send(new DeleteVehicleCatalogEntryCommand(parsed, id), ct);
                return result.ToHttpResult(_ => Results.NoContent());
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("DeleteVehicleCatalogEntry")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    // Enum.TryParse alone would also accept numeric strings such as "7".
    private static bool TryParseKind(string value, out VehicleCatalogKind kind)
    {
        kind = default;
        return !value.Any(char.IsDigit) && Enum.TryParse(value, ignoreCase: true, out kind) && Enum.IsDefined(kind);
    }

    private static IResult UnknownKind(string kind) => Results.Problem(
        title: "VehicleCatalog.UnknownKind",
        detail: $"'{kind}' is not a known vehicle catalog. Use 'VehicleClass' or 'CarrierGroup'.",
        statusCode: StatusCodes.Status400BadRequest);
}

/// <summary>Body of a new catalog entry. The kind comes from the route.</summary>
public sealed record CreateVehicleCatalogEntryRequest(string Code, string Name, bool IsActive = true);

/// <summary>Body of a catalog entry update. The code cannot be changed.</summary>
public sealed record UpdateVehicleCatalogEntryRequest(string Name, bool IsActive);
