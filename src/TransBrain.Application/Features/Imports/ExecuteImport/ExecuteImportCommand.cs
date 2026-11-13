using TransBrain.Application.Common.Messaging;

namespace TransBrain.Application.Features.Imports.ExecuteImport;

/// <summary>
/// Runs the import: every source row goes through the mapping and then through the target
/// entity's regular create command. Rows commit one by one — a failing row is reported and
/// skipped, it does not roll back the rows before it.
/// </summary>
public sealed record ExecuteImportCommand(
    Guid SessionId,
    string TableName,
    string TargetEntity,
    IReadOnlyList<ImportFieldMapping> Mappings) : ICommand<ImportExecutionResponse>;

public sealed record ImportRowErrorResponse(int RowNumber, string Message);

public sealed record ImportExecutionResponse(
    Guid SessionId,
    string Status,
    int TotalRows,
    int ImportedCount,
    int FailedCount,
    IReadOnlyList<ImportRowErrorResponse> Errors);
