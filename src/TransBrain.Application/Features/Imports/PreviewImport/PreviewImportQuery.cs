using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Imports.PreviewImport;

/// <summary>
/// Dry-runs a mapping: converts every row without writing anything, and returns the first
/// <see cref="MaxRows"/> rows in detail plus overall validity counts.
/// </summary>
public sealed record PreviewImportQuery(
    Guid SessionId,
    string TableName,
    string TargetEntity,
    IReadOnlyList<ImportFieldMapping> Mappings,
    int MaxRows = 20) : IQuery<ImportPreviewResponse>;

public sealed record ImportPreviewRowResponse(
    int RowNumber,
    IReadOnlyDictionary<string, string?> Values,
    IReadOnlyList<string> Errors);

public sealed record ImportPreviewResponse(
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    IReadOnlyList<ImportPreviewRowResponse> Rows);
