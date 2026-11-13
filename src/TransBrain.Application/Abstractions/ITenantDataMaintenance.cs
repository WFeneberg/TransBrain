namespace TransBrain.Application.Abstractions;

/// <summary>
/// Export and import of one tenant's complete data set — the seam behind the admin-only
/// maintenance (Wartung) feature. Implemented in Infrastructure, because a faithful copy works
/// on tables and rows, not on aggregates.
/// </summary>
/// <remarks>
/// Both operations scope strictly to <see cref="ITenantContext.TenantId"/>: export never reads
/// and import never writes another tenant's rows, no matter what an uploaded file claims.
/// </remarks>
public interface ITenantDataMaintenance
{
    /// <summary>Takes a consistent snapshot of every tenant-owned row.</summary>
    Task<TenantBackupDocument> ExportAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the current tenant's data with the backup's contents, atomically: on any
    /// failure the previous state survives untouched. Returns restored row counts per table.
    /// </summary>
    /// <exception cref="TenantBackupDataException">
    /// The file's rows do not fit the current schema or violate integrity (the caller has
    /// already verified format, tenant and schema version).
    /// </exception>
    Task<IReadOnlyDictionary<string, int>> ImportAsync(
        TenantBackupDocument backup, CancellationToken cancellationToken);

    /// <summary>The latest applied EF Core migration id — the schema version backups pin to.</summary>
    Task<string> GetSchemaVersionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// An uploaded backup file whose row data cannot be restored: unknown tables, malformed rows,
/// duplicate keys or references into another tenant's data. Carries a caller-safe message.
/// </summary>
public sealed class TenantBackupDataException(string message, Exception? innerException = null)
    : Exception(message, innerException);
