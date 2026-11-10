using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Customers;
using TransBrain.Application.Features.Customers.CreateCustomer;
using TransBrain.Application.Features.Customers.DeleteCustomer;
using TransBrain.Application.Features.Customers.GetCustomerById;
using TransBrain.Application.Features.Customers.ListCustomers;
using TransBrain.Application.Features.Customers.UpdateCustomer;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

public sealed class CustomerEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/customers").WithTags("Customers");

        group.MapPost("/", async (CreateCustomerCommand command, ISender sender, CancellationToken ct) =>
            {
                Result<CustomerResponse> result = await sender.Send(command, ct);
                return result.ToHttpResult(customer => Results.Created($"/api/customers/{customer.Id}", customer));
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("CreateCustomer")
            .Produces<CustomerResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/", async (ISender sender, CancellationToken ct, int page = 1, int pageSize = 20) =>
            {
                Result<PagedResult<CustomerResponse>> result =
                    await sender.Send(new ListCustomersQuery(page, pageSize), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListCustomers")
            .Produces<PagedResult<CustomerResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<CustomerResponse> result = await sender.Send(new GetCustomerByIdQuery(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetCustomerById")
            .Produces<CustomerResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", async (
                Guid id, UpdateCustomerRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<CustomerResponse> result = await sender.Send(
                    new UpdateCustomerCommand(
                        id,
                        request.Name,
                        request.Street,
                        request.PostalCode,
                        request.City,
                        request.Country,
                        request.ContactPerson,
                        request.Email,
                        request.Phone,
                        request.VatId,
                        request.PaymentTermDays,
                        request.Additional),
                    ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("UpdateCustomer")
            .Produces<CustomerResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<Unit> result = await sender.Send(new DeleteCustomerCommand(id), ct);
                return result.ToHttpResult(_ => Results.NoContent());
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("DeleteCustomer")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

/// <summary>
/// Body of a customer update. The id comes from the route, not the payload. A missing
/// <see cref="Additional"/> clears the additional data, like on create.
/// </summary>
public sealed record UpdateCustomerRequest(
    string Name,
    string Street,
    string PostalCode,
    string City,
    string Country,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? VatId,
    int PaymentTermDays,
    CustomerAdditionalData? Additional = null);
