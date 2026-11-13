using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Customers;
using TransBrain.Application.Features.Drivers;
using TransBrain.Application.Features.Routing;
using TransBrain.Application.Features.Vehicles;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Maintenance.RestoreTenantData;

/// <remarks>
/// Order of the gates matters for the quality of the answer: format first (is this even one of
/// our files), then tenant (is it YOUR file), then schema (does it fit THIS server) — each gives
/// the admin a different fix. Only then does the import touch the database, atomically. The
/// tenant gate also keeps restore from becoming a cross-tenant copy tool: row ids are globally
/// unique, so restoring another tenant's live data would collide on primary keys halfway through
/// instead of failing with a reason.
/// </remarks>
internal sealed class RestoreTenantDataCommandHandler(
    ITenantDataMaintenance maintenance,
    ITenantContext tenantContext,
    ICacheService cache) : ICommandHandler<RestoreTenantDataCommand, RestoreTenantDataResponse>
{
    public async Task<Result<RestoreTenantDataResponse>> Handle(
        RestoreTenantDataCommand command, CancellationToken cancellationToken)
    {
        TenantBackupDocument backup = command.Backup;

        if (backup.FormatVersion != TenantBackupDocument.Version)
        {
            return Error.Validation(
                "Backup.UnsupportedFormatVersion",
                $"The backup uses format version {backup.FormatVersion}; this server reads version {TenantBackupDocument.Version}.");
        }

        if (backup.TenantId != tenantContext.TenantId)
        {
            return Error.Validation(
                "Backup.TenantMismatch",
                "The backup belongs to a different tenant. A tenant can only restore its own backups.");
        }

        string schemaVersion = await maintenance.GetSchemaVersionAsync(cancellationToken);
        if (!string.Equals(backup.SchemaVersion, schemaVersion, StringComparison.Ordinal))
        {
            return Error.Conflict(
                "Backup.SchemaMismatch",
                $"The backup was taken on database schema '{backup.SchemaVersion}', but this server runs "
                + $"'{schemaVersion}'. Restore a backup taken on the current version.");
        }

        IReadOnlyDictionary<string, int> restored;
        try
        {
            restored = await maintenance.ImportAsync(backup, cancellationToken);
        }
        catch (TenantBackupDataException exception)
        {
            return Error.Validation("Backup.InvalidData", exception.Message);
        }

        // Every cached read of this tenant may describe the replaced data. The cache service
        // scopes these prefixes to the current tenant, so other tenants' entries survive.
        await cache.RemoveByPrefixAsync(VehicleCacheKeys.Prefix, cancellationToken);
        await cache.RemoveByPrefixAsync(DriverCacheKeys.Prefix, cancellationToken);
        await cache.RemoveByPrefixAsync(CustomerCacheKeys.Prefix, cancellationToken);
        await cache.RemoveByPrefixAsync(GeocodeCache.Prefix, cancellationToken);

        return new RestoreTenantDataResponse(
            backup.SchemaVersion,
            backup.CreatedAt,
            restored.Values.Sum(),
            restored);
    }
}
