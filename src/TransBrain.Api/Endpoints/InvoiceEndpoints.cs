using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Common.Pagination;
using TransBrain.Application.Features.Invoices;
using TransBrain.Application.Features.Invoices.CancelInvoice;
using TransBrain.Application.Features.Invoices.CreateInvoice;
using TransBrain.Application.Features.Invoices.GetInvoiceById;
using TransBrain.Application.Features.Invoices.IssueInvoice;
using TransBrain.Application.Features.Invoices.ListBillableOrders;
using TransBrain.Application.Features.Invoices.ListInvoices;
using TransBrain.Application.Features.Invoices.MarkInvoicePaid;
using TransBrain.Application.Features.Orders;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

/// <remarks>
/// Writes use Policies.DispatchWrite (admin and disponent), like orders: billing closes the
/// dispatch workflow commercially, and there is no separate accounting role in the realm.
/// </remarks>
public sealed class InvoiceEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/invoices").WithTags("Invoices");

        group.MapPost("/", async (CreateInvoiceCommand command, ISender sender, CancellationToken ct) =>
            {
                Result<InvoiceResponse> result = await sender.Send(command, ct);
                return result.ToHttpResult(invoice => Results.Created($"/api/invoices/{invoice.Id}", invoice));
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("CreateInvoice")
            .Produces<InvoiceResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/", async (
                ISender sender,
                CancellationToken ct,
                int page = 1,
                int pageSize = 20,
                string? status = null,
                Guid? customerId = null) =>
            {
                Result<PagedResult<InvoiceResponse>> result = await sender.Send(
                    new ListInvoicesQuery(page, pageSize, status, customerId), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListInvoices")
            .Produces<PagedResult<InvoiceResponse>>()
            .ProducesValidationProblem();

        // Before "/{id:guid}" in reading order, but no route conflict either way: the segment is
        // not a guid. The billing screen uses this to offer what can still be billed.
        group.MapGet("/billable-orders", async (ISender sender, CancellationToken ct) =>
            {
                Result<IReadOnlyList<OrderResponse>> result = await sender.Send(
                    new ListBillableOrdersQuery(), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("ListBillableOrders")
            .Produces<IReadOnlyList<OrderResponse>>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<InvoiceResponse> result = await sender.Send(new GetInvoiceByIdQuery(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetInvoiceById")
            .Produces<InvoiceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/issue", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<InvoiceResponse> result = await sender.Send(new IssueInvoiceCommand(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("IssueInvoice")
            .Produces<InvoiceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/pay", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<InvoiceResponse> result = await sender.Send(new MarkInvoicePaidCommand(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("MarkInvoicePaid")
            .Produces<InvoiceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // Cancelling is a state transition, not a deletion: a haulier keeps the record of an
        // invoice that was written and voided. Hence POST to a sub-resource rather than DELETE.
        group.MapPost("/{id:guid}/cancel", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<InvoiceResponse> result = await sender.Send(new CancelInvoiceCommand(id), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.DispatchWrite)
            .WithName("CancelInvoice")
            .Produces<InvoiceResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
