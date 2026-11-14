using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.InvoiceDesigns;
using TransBrain.Application.Features.InvoiceDesigns.GetInvoiceDesign;
using TransBrain.Application.Features.InvoiceDesigns.PreviewInvoiceDesign;
using TransBrain.Application.Features.InvoiceDesigns.UpdateInvoiceDesign;
using TransBrain.Application.Features.Invoices;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

/// <remarks>
/// A singleton per-tenant resource, hence no ids in the routes. Writing the design — the
/// tenant's own company data and letterhead — is master data maintenance and stays with the
/// admin; reading uses the general read policy, because every invoice PDF already shows the
/// same data to every role that may open invoices.
/// </remarks>
public sealed class InvoiceDesignEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/invoice-design").WithTags("InvoiceDesign");

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            {
                Result<InvoiceDesignResponse> result = await sender.Send(new GetInvoiceDesignQuery(), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Read)
            .WithName("GetInvoiceDesign")
            .Produces<InvoiceDesignResponse>();

        group.MapPut("/", async (InvoiceDesignPayload payload, ISender sender, CancellationToken ct) =>
            {
                Result<InvoiceDesignResponse> result = await sender.Send(new UpdateInvoiceDesignCommand(payload), ct);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("UpdateInvoiceDesign")
            .Produces<InvoiceDesignResponse>()
            .ProducesValidationProblem();

        // A read in disguise of a POST: the unsaved design travels in the body because the
        // preview must show exactly what the designer currently edits, saved or not.
        group.MapPost("/preview", async (InvoiceDesignPayload payload, ISender sender, CancellationToken ct) =>
            {
                Result<InvoiceDocumentResponse> result = await sender.Send(new PreviewInvoiceDesignQuery(payload), ct);
                return result.ToHttpResult(doc => Results.File(doc.Content, doc.ContentType, doc.FileName));
            })
            .RequireAuthorization(Policies.MasterDataWrite)
            .WithName("PreviewInvoiceDesign")
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .ProducesValidationProblem();
    }
}
