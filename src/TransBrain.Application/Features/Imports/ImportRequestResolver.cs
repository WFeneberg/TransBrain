using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Imports.Targets;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Features.Imports;

/// <summary>Everything preview and execution need, resolved and validated once.</summary>
internal sealed record ResolvedImportRequest(
    ImportSession Session,
    ImportTable Table,
    IImportTargetDefinition Target,
    IReadOnlyList<ResolvedFieldMapping> Mappings);

/// <summary>
/// Shared front door of preview and execution: loads the session, finds the source table and the
/// target definition, and resolves the mapping — so both operations accept and reject exactly the
/// same requests.
/// </summary>
internal static class ImportRequestResolver
{
    public static async Task<Result<ResolvedImportRequest>> ResolveAsync(
        IImportSessionRepository repository,
        Guid sessionId,
        string tableName,
        string targetEntity,
        IReadOnlyList<ImportFieldMapping> mappings,
        CancellationToken cancellationToken)
    {
        ImportSession? session = await repository.GetByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return Error.NotFound("Import.SessionNotFound", $"Import session '{sessionId}' was not found.");
        }

        ImportTableSet? tables = await repository.GetTableSetAsync(sessionId, cancellationToken);
        if (tables is null || tables.Tables.Count == 0)
        {
            return Error.NotFound(
                "Import.DataNotFound", $"The analyzed data of import session '{sessionId}' is no longer available.");
        }

        ImportTable? table = tables.Tables
            .FirstOrDefault(t => string.Equals(t.Name, tableName, StringComparison.OrdinalIgnoreCase));
        if (table is null)
        {
            return Error.Validation(
                "Import.UnknownTable",
                $"Table '{tableName}' does not exist in this import session. " +
                $"Available: {string.Join(", ", tables.Tables.Select(t => t.Name))}.");
        }

        IImportTargetDefinition? target = ImportTargetRegistry.Find(targetEntity);
        if (target is null)
        {
            return Error.Validation(
                "Import.UnknownTarget",
                $"'{targetEntity}' is not an import target. " +
                $"Available: {string.Join(", ", ImportTargetRegistry.All.Select(t => t.Entity))}.");
        }

        Result<IReadOnlyList<ResolvedFieldMapping>> resolved = ImportRowConverter.Resolve(table, target, mappings);
        if (!resolved.IsSuccess)
        {
            return resolved.Error!;
        }

        return new ResolvedImportRequest(session, table, target, resolved.Value);
    }
}
