using TransBrain.Domain.Imports;

namespace TransBrain.Application.Features.Imports;

/// <summary>One analyzed source table, with a handful of sample rows for the mapping UI.</summary>
public sealed record ImportTableResponse(
    string Name,
    IReadOnlyList<string> Columns,
    int RowCount,
    IReadOnlyList<IReadOnlyList<string?>> SampleRows);

/// <summary>An import session. <see cref="Tables"/> is null in list views.</summary>
public sealed record ImportSessionResponse(
    Guid Id,
    string SourceType,
    string SourceName,
    string Status,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? TargetEntity,
    string? TableName,
    int ImportedCount,
    int FailedCount,
    IReadOnlyList<ImportTableResponse>? Tables)
{
    public static ImportSessionResponse From(ImportSession session, ImportTableSet? tables = null)
        => new(
            session.Id,
            session.SourceType.ToString(),
            session.SourceName,
            session.Status.ToString(),
            session.CreatedBy,
            session.CreatedAtUtc,
            session.CompletedAtUtc,
            session.TargetEntity,
            session.TableName,
            session.ImportedCount,
            session.FailedCount,
            tables is null ? null : tables.Tables.Select(ToTableResponse).ToList());

    private static ImportTableResponse ToTableResponse(ImportTable table)
        => new(
            table.Name,
            table.Columns,
            table.Rows.Count,
            table.Rows.Take(ImportLimits.SampleRows).ToList());
}
