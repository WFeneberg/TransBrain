using System.Text.Json;

namespace TransBrain.Application.Abstractions;

/// <summary>
/// A complete, self-contained snapshot of one tenant's (Mandant's) data, shaped for download as
/// a JSON file and for upload on restore.
/// </summary>
/// <remarks>
/// The rows travel as raw <see cref="JsonElement"/> arrays, one per physical table, exactly as
/// the database produced them. The Application layer never interprets them: a backup is a
/// faithful copy, not a domain operation, and interpreting rows here would silently couple this
/// format to the entity classes. <see cref="SchemaVersion"/> pins the file to the database
/// schema (the latest applied EF migration) it was taken from — restore refuses any other
/// schema instead of guessing how columns moved.
/// </remarks>
/// <param name="FileFormat">Always <see cref="Format"/>; lets restore reject arbitrary JSON early.</param>
/// <param name="FormatVersion">Version of this envelope, not of the schema; see <see cref="Version"/>.</param>
/// <param name="SchemaVersion">The latest applied EF Core migration id at export time.</param>
/// <param name="TenantId">The tenant the data belongs to; restore refuses another tenant's file.</param>
/// <param name="CreatedAt">When the backup was taken.</param>
/// <param name="Tables">Rows per physical table name, each value a JSON array of row objects.</param>
public sealed record TenantBackupDocument(
    string FileFormat,
    int FormatVersion,
    string SchemaVersion,
    Guid TenantId,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, JsonElement> Tables)
{
    public const string Format = "transbrain-tenant-backup";

    public const int Version = 1;
}
