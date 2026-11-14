using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Exports;
using TransBrain.Application.Features.Exports.ExportData;
using TransBrain.Application.Features.Exports.GetExportTargets;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

/// <summary>
/// Master-data export as a download: the same entities the import knows (customers, drivers,
/// vehicles) written as CSV, JSON or Excel. The column vocabulary matches the import, so an
/// exported file can be re-imported with every column auto-matched. Tenant-scoped and reserved
/// for master-data administrators, like the import.
/// </summary>
public sealed class ExportEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/exports")
            .WithTags("Exports")
            .RequireAuthorization(Policies.MasterDataWrite);

        group.MapGet("/targets", async (ISender sender, CancellationToken ct) =>
            {
                Result<IReadOnlyList<ExportTargetResponse>> result =
                    await sender.Send(new GetExportTargetsQuery(), ct);
                return result.ToHttpResult();
            })
            .WithName("GetExportTargets")
            .WithSummary("Lists the exportable entities with their current row counts.")
            .Produces<IReadOnlyList<ExportTargetResponse>>();

        group.MapGet("/{entity}", async (string entity, string? format, ISender sender, CancellationToken ct) =>
            {
                Result<ExportFileResult> result =
                    await sender.Send(new ExportDataQuery(entity, format ?? nameof(ExportFormat.Csv)), ct);
                return result.ToHttpResult(file => Results.File(
                    file.Content,
                    contentType: file.ContentType,
                    fileDownloadName: file.FileName));
            })
            .WithName("ExportData")
            .WithSummary("Downloads all rows of one entity as CSV, JSON or Excel.")
            .WithDescription("`format` is Csv (default), Json or Xlsx. "
                             + "The columns match the import, so the file can be imported again as-is.")
            .Produces(StatusCodes.Status200OK)
            .ProducesValidationProblem();
    }
}
