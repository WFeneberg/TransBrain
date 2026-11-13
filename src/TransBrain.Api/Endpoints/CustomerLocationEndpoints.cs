using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Customers.CustomerLocations;
using TransBrain.Application.Features.Customers.CustomerLocations.AddCustomerLocation;
using TransBrain.Application.Features.Customers.CustomerLocations.DeleteCustomerLocation;
using TransBrain.Application.Features.Customers.CustomerLocations.GetCustomerLocationPosition;
using TransBrain.Application.Features.Customers.CustomerLocations.ListAllCustomerLocations;
using TransBrain.Application.Features.Customers.CustomerLocations.ListCustomerLocations;
using TransBrain.Application.Features.Customers.CustomerLocations.UpdateCustomerLocation;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

public sealed class CustomerLocationEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/customers/{customerId:guid}/locations")
            .WithTags("Customers");

        group.MapGet("/", async (Guid customerId, ISender sender, CancellationToken ct) =>
            {
                Result<CustomerLocationResponse[]> result =
                    await sender.Send(new ListCustomerLocationsQuery(customerId), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListCustomerLocations")
            .Produces<CustomerLocationResponse[]>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (
                Guid customerId, AddCustomerLocationRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<CustomerLocationResponse> result = await sender.Send(
                    new AddCustomerLocationCommand(
                        customerId,
                        request.Name,
                        request.Street,
                        request.PostalCode,
                        request.City,
                        request.Country,
                        request.CompanyName,
                        request.VatId,
                        request.Contacts),
                    ct);
                return result.ToHttpResult(location => Results.Created(
                    $"/api/customers/{customerId}/locations/{location.Id}", location));
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("AddCustomerLocation")
            .Produces<CustomerLocationResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{locationId:guid}", async (
                Guid customerId, Guid locationId, UpdateCustomerLocationRequest request,
                ISender sender, CancellationToken ct) =>
            {
                Result<CustomerLocationResponse> result = await sender.Send(
                    new UpdateCustomerLocationCommand(
                        customerId,
                        locationId,
                        request.Name,
                        request.Street,
                        request.PostalCode,
                        request.City,
                        request.Country,
                        request.CompanyName,
                        request.VatId,
                        request.Contacts),
                    ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("UpdateCustomerLocation")
            .Produces<CustomerLocationResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{locationId:guid}/position", async (Guid customerId, Guid locationId, ISender sender, CancellationToken ct) =>
            {
                Result<CustomerLocationPositionResponse> result =
                    await sender.Send(new GetCustomerLocationPositionQuery(customerId, locationId), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetCustomerLocationPosition")
            .Produces<CustomerLocationPositionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapDelete("/{locationId:guid}", async (Guid customerId, Guid locationId, ISender sender, CancellationToken ct) =>
            {
                Result<Unit> result = await sender.Send(new DeleteCustomerLocationCommand(customerId, locationId), ct);
                return result.ToHttpResult(_ => Results.NoContent());
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("DeleteCustomerLocation")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Across all customers: the order form picks a delivery location without knowing its customer first.
        RouteGroupBuilder allLocations = app.MapGroup("/api/customer-locations").WithTags("Customers");

        allLocations.MapGet("/", async (ISender sender, CancellationToken ct, int page = 1, int pageSize = 100) =>
            {
                Result<PagedResult<CustomerLocationListItem>> result =
                    await sender.Send(new ListAllCustomerLocationsQuery(page, pageSize), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListAllCustomerLocations")
            .Produces<PagedResult<CustomerLocationListItem>>()
            .ProducesValidationProblem();
    }
}

/// <summary>Body of a delivery location create. The customer id comes from the route.</summary>
public sealed record AddCustomerLocationRequest(
    string Name,
    string Street,
    string PostalCode,
    string City,
    string Country,
    string? CompanyName = null,
    string? VatId = null,
    IReadOnlyList<CustomerLocationContactData>? Contacts = null);

/// <summary>Body of a delivery location update. The ids come from the route; <c>contacts</c> replaces the list as a whole.</summary>
public sealed record UpdateCustomerLocationRequest(
    string Name,
    string Street,
    string PostalCode,
    string City,
    string Country,
    string? CompanyName = null,
    string? VatId = null,
    IReadOnlyList<CustomerLocationContactData>? Contacts = null);
