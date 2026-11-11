using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Geodata;
using TransBrain.Application.Features.Geodata.DeleteGeodataOverride;
using TransBrain.Application.Features.Geodata.GetMapDataStatus;
using TransBrain.Application.Features.Geodata.ListGeodata;
using TransBrain.Application.Features.Geodata.ResolveGeodata;
using TransBrain.Application.Features.Geodata.SetGeodataOverride;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

/// <summary>
/// Geodata maintenance: every address the system routes to, with the position it resolves to.
/// Operators supplement addresses the geocoder does not know and correct positions that are wrong.
/// </summary>
public sealed class GeodataEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/geodata").WithTags("Geodata");

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            {
                Result<GeodataEntryResponse[]> result = await sender.Send(new ListGeodataQuery(), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListGeodata")
            .Produces<GeodataEntryResponse[]>();

        // The map extract is updated on the server (see the operator guide); this only shows
        // whether the self-hosted services are up and how old their OSM data is.
        group.MapGet("/map-status", async (ISender sender, CancellationToken ct) =>
            {
                Result<MapDataStatusResponse> result = await sender.Send(new GetMapDataStatusQuery(), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetMapDataStatus")
            .Produces<MapDataStatusResponse>();

        group.MapPut("/overrides", async (SetGeodataOverrideRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<GeodataOverrideResponse> result = await sender.Send(
                    new SetGeodataOverrideCommand(
                        request.Street,
                        request.PostalCode,
                        request.City,
                        request.Country,
                        request.Latitude,
                        request.Longitude),
                    ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("SetGeodataOverride")
            .Produces<GeodataOverrideResponse>()
            .ProducesValidationProblem();

        group.MapDelete("/overrides/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<Unit> result = await sender.Send(new DeleteGeodataOverrideCommand(id), ct);
                return result.ToHttpResult(_ => Results.NoContent());
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("DeleteGeodataOverride")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // A POST, not a GET: a successful probe fills the geocode cache.
        group.MapPost("/resolve", async (ResolveGeodataRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<GeodataPositionResponse> result = await sender.Send(
                    new ResolveGeodataCommand(request.Street, request.PostalCode, request.City, request.Country),
                    ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ResolveGeodata")
            .Produces<GeodataPositionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
    }
}

/// <summary>Body of a manual position upsert. The address itself is the key.</summary>
public sealed record SetGeodataOverrideRequest(
    string Street,
    string PostalCode,
    string City,
    string Country,
    double Latitude,
    double Longitude);

/// <summary>Body of a resolution probe.</summary>
public sealed record ResolveGeodataRequest(
    string Street,
    string PostalCode,
    string City,
    string Country);
