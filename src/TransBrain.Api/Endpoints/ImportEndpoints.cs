using TransBrain.Api.Common;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Imports;
using TransBrain.Application.Features.Imports.CreateImportSession;
using TransBrain.Application.Features.Imports.DeleteImportSession;
using TransBrain.Application.Features.Imports.ExecuteImport;
using TransBrain.Application.Features.Imports.GetImportSession;
using TransBrain.Application.Features.Imports.GetImportTargets;
using TransBrain.Application.Features.Imports.ListImportSessions;
using TransBrain.Application.Features.Imports.PreviewImport;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

/// <summary>
/// Data import from other programs: upload a file (CSV, JSON, XML, Excel, Access, SQLite) or
/// connect to a live database, then map the analyzed tables onto TransBrain entities, preview,
/// and execute. Lives in the global system area: a system administrator picks the target tenant
/// in the route, and everything below runs scoped to that tenant.
/// </summary>
public sealed class ImportEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapSystemTenantGroup("/imports").WithTags("Imports");

        group.MapGet("/targets", async (ISender sender, CancellationToken ct) =>
            {
                Result<IReadOnlyList<ImportTargetResponse>> result =
                    await sender.Send(new GetImportTargetsQuery(), ct);
                return result.ToHttpResult();
            })
            .WithName("GetImportTargets")
            .Produces<IReadOnlyList<ImportTargetResponse>>();

        group.MapPost("/files", async (Guid tenantId, IFormFile file, ISender sender, CancellationToken ct) =>
            {
                if (file.Length > ImportLimits.MaxFileBytes)
                {
                    return Results.Problem(
                        title: "Import.FileTooLarge",
                        detail: $"The file exceeds the limit of {ImportLimits.MaxFileBytes / (1024 * 1024)} MB.",
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                }

                byte[] content;
                using (var buffer = new MemoryStream((int)file.Length))
                {
                    await file.CopyToAsync(buffer, ct);
                    content = buffer.ToArray();
                }

                Result<ImportSessionResponse> result = await sender.Send(
                    new CreateFileImportSessionCommand(file.FileName, content), ct);
                return result.ToHttpResult(session =>
                    Results.Created($"/api/system/tenants/{tenantId}/imports/{session.Id}", session));
            })
            .DisableAntiforgery() // Token-authenticated JSON API; no cookie-based form posts.
            .WithName("CreateFileImportSession")
            .Produces<ImportSessionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        group.MapPost("/databases", async (
                Guid tenantId, CreateDatabaseImportSessionRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<ImportSessionResponse> result = await sender.Send(
                    new CreateDatabaseImportSessionCommand(request.Provider, request.ConnectionString), ct);
                return result.ToHttpResult(session =>
                    Results.Created($"/api/system/tenants/{tenantId}/imports/{session.Id}", session));
            })
            .WithName("CreateDatabaseImportSession")
            .Produces<ImportSessionResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
            {
                Result<IReadOnlyList<ImportSessionResponse>> result =
                    await sender.Send(new ListImportSessionsQuery(), ct);
                return result.ToHttpResult();
            })
            .WithName("ListImportSessions")
            .Produces<IReadOnlyList<ImportSessionResponse>>();

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<ImportSessionResponse> result = await sender.Send(new GetImportSessionQuery(id), ct);
                return result.ToHttpResult();
            })
            .WithName("GetImportSession")
            .Produces<ImportSessionResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/preview", async (
                Guid id, PreviewImportRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<ImportPreviewResponse> result = await sender.Send(
                    new PreviewImportQuery(
                        id,
                        request.TableName,
                        request.TargetEntity,
                        request.Mappings,
                        request.MaxRows ?? 20),
                    ct);
                return result.ToHttpResult();
            })
            .WithName("PreviewImport")
            .Produces<ImportPreviewResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/execute", async (
                Guid id, ExecuteImportRequest request, ISender sender, CancellationToken ct) =>
            {
                Result<ImportExecutionResponse> result = await sender.Send(
                    new ExecuteImportCommand(id, request.TableName, request.TargetEntity, request.Mappings), ct);
                return result.ToHttpResult();
            })
            .WithName("ExecuteImport")
            .Produces<ImportExecutionResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                Result<Unit> result = await sender.Send(new DeleteImportSessionCommand(id), ct);
                return result.ToHttpResult(_ => Results.NoContent());
            })
            .WithName("DeleteImportSession")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}

/// <summary>A live-database import source. The connection string is used once and never stored.</summary>
public sealed record CreateDatabaseImportSessionRequest(string Provider, string ConnectionString);

/// <summary>Mapping test run. <c>MaxRows</c> limits the returned row details, not the counted total.</summary>
public sealed record PreviewImportRequest(
    string TableName,
    string TargetEntity,
    IReadOnlyList<ImportFieldMapping> Mappings,
    int? MaxRows);

/// <summary>Runs the import; each row is created through the same validation as manual entry.</summary>
public sealed record ExecuteImportRequest(
    string TableName,
    string TargetEntity,
    IReadOnlyList<ImportFieldMapping> Mappings);
