using System.Text.Json;
using TransBrain.Api.Common;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Maintenance.BackupTenantData;
using TransBrain.Application.Features.Maintenance.ListTenants;
using TransBrain.Application.Features.Maintenance.RestoreTenantData;
using TransBrain.Domain.Common;

namespace TransBrain.Api.Endpoints;

/// <summary>
/// The global Wartung (maintenance) endpoints: list all tenants, back up and restore any one of
/// them. System-admin only by policy; which tenant is affected comes from the route — system
/// administrators belong to no tenant themselves.
/// </summary>
public sealed class SystemMaintenanceEndpoints : IEndpointGroup
{
    /// <summary>
    /// Also used when serializing the download, so an uploaded backup binds through exactly the
    /// same contract (camelCase, case-insensitive) it was written with.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Map(IEndpointRouteBuilder app)
    {
        app.MapSystemGroup().WithTags("System maintenance")
            .MapGet("/tenants", async (ISender sender, CancellationToken cancellationToken) =>
            {
                Result<IReadOnlyList<TenantSummary>> result =
                    await sender.Send(new ListTenantsQuery(), cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("ListTenants")
            .WithSummary("Lists every tenant that owns data, with headline row counts.")
            .Produces<IReadOnlyList<TenantSummary>>();

        RouteGroupBuilder tenant = app.MapSystemTenantGroup().WithTags("System maintenance");

        tenant.MapGet("/backup", async (ISender sender, CancellationToken cancellationToken) =>
            {
                Result<TenantBackupDocument> result =
                    await sender.Send(new BackupTenantDataQuery(), cancellationToken);
                return result.ToHttpResult(backup => Results.File(
                    JsonSerializer.SerializeToUtf8Bytes(backup, SerializerOptions),
                    contentType: "application/json",
                    fileDownloadName: $"transbrain-backup-{backup.CreatedAt:yyyyMMdd-HHmmss}.json"));
            })
            .WithName("BackupTenantData")
            .WithSummary("Downloads a complete backup of the chosen tenant's data as a JSON file.")
            .WithDescription("The file is tied to the tenant and to the database schema version it was taken on.")
            .Produces<TenantBackupDocument>(contentType: "application/json");

        tenant.MapPost("/restore", async (
                TenantBackupDocument backup, ISender sender, CancellationToken cancellationToken) =>
            {
                Result<RestoreTenantDataResponse> result =
                    await sender.Send(new RestoreTenantDataCommand(backup), cancellationToken);
                return result.ToHttpResult();
            })
            .WithName("RestoreTenantData")
            .WithSummary("Replaces ALL of the chosen tenant's data with the uploaded backup file.")
            .WithDescription("Atomic: on any error the current data stays untouched. "
                             + "Refuses a file taken from a different tenant (400) and a foreign schema version (409).")
            .Produces<RestoreTenantDataResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
