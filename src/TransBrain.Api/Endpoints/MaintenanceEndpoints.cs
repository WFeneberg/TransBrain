using System.Text.Json;
using TransBrain.Api.Authorization;
using TransBrain.Api.Common;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Maintenance.BackupTenantData;
using TransBrain.Application.Features.Maintenance.RestoreTenantData;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

/// <summary>
/// The Wartung (maintenance) endpoints: backup and restore of the calling tenant's data.
/// Admin-only by policy; which tenant is affected comes from the token, never from the file.
/// </summary>
public sealed class MaintenanceEndpoints : IEndpointGroup
{
    /// <summary>
    /// Also used when serializing the download, so an uploaded backup binds through exactly the
    /// same contract (camelCase, case-insensitive) it was written with.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Map(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/maintenance").WithTags("Maintenance");

        group.MapGet("/backup", async (ISender sender, CancellationToken cancellationToken) =>
            {
                Result<TenantBackupDocument> result =
                    await sender.Send(new BackupTenantDataQuery(), cancellationToken);
                return result.ToHttpResult(backup => Results.File(
                    JsonSerializer.SerializeToUtf8Bytes(backup, SerializerOptions),
                    contentType: "application/json",
                    fileDownloadName: $"transbrain-backup-{backup.CreatedAt:yyyyMMdd-HHmmss}.json"));
            })
            .RequireAuthorization(Policies.Maintenance)
            .WithName("BackupTenantData")
            .WithSummary("Downloads a complete backup of the tenant's data as a JSON file.")
            .WithDescription("The file is tied to the tenant and to the database schema version it was taken on.")
            .Produces<TenantBackupDocument>(contentType: "application/json");

        group.MapPost("/restore", async (
                TenantBackupDocument backup, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<RestoreTenantDataResponse> result =
                    await sender.Send(new RestoreTenantDataCommand(backup), cancellationToken);
                return result.ToHttpResult();
            })
            .RequireAuthorization(Policies.Maintenance)
            .WithName("RestoreTenantData")
            .WithSummary("Replaces ALL of the tenant's data with the uploaded backup file.")
            .WithDescription("Atomic: on any error the current data stays untouched. "
                             + "Refuses another tenant's file (400) and a foreign schema version (409).")
            .Produces<RestoreTenantDataResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
