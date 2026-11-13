namespace TransBrain.Application.Features.Maintenance.RestoreTenantData;

/// <summary>What a completed restore replaced, for the maintenance page to show.</summary>
/// <param name="SchemaVersion">The schema version both the backup and the server run.</param>
/// <param name="BackupCreatedAt">When the restored backup had been taken.</param>
/// <param name="TotalRows">Total restored rows across all tables.</param>
/// <param name="Tables">Restored row count per physical table.</param>
public sealed record RestoreTenantDataResponse(
    string SchemaVersion,
    DateTimeOffset BackupCreatedAt,
    int TotalRows,
    IReadOnlyDictionary<string, int> Tables);
