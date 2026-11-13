using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Application.Features.Imports.Targets;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Features.Imports.ExecuteImport;

internal sealed class ExecuteImportCommandHandler(
    IImportSessionRepository repository,
    ISender sender,
    TimeProvider timeProvider)
    : ICommandHandler<ExecuteImportCommand, ImportExecutionResponse>
{
    public async Task<Result<ImportExecutionResponse>> Handle(
        ExecuteImportCommand command,
        CancellationToken cancellationToken)
    {
        Result<ResolvedImportRequest> resolved = await ImportRequestResolver.ResolveAsync(
            repository, command.SessionId, command.TableName, command.TargetEntity, command.Mappings,
            cancellationToken);
        if (!resolved.IsSuccess)
        {
            return resolved.Error!;
        }

        (ImportSession session, ImportTable table, IImportTargetDefinition target,
            IReadOnlyList<ResolvedFieldMapping> mappings) = resolved.Value;

        int imported = 0;
        int failed = 0;
        var errors = new List<ImportRowErrorResponse>();

        for (int i = 0; i < table.Rows.Count; i++)
        {
            // Deliberately not cancellable between rows via silent break: a cancelled request
            // aborts the loop through the token in the inner send, and everything imported so
            // far stays imported — per-row commits are the contract of this feature.
            int rowNumber = i + 1;

            ImportRowConversion conversion = ImportRowConverter.Convert(mappings, table.Rows[i]);
            if (!conversion.IsValid)
            {
                failed++;
                AddError(errors, rowNumber, string.Join(" ", conversion.Errors));
                continue;
            }

            Result<Unit> rowResult = await target.ImportRowAsync(sender, conversion.Values!, cancellationToken);
            if (rowResult.IsSuccess)
            {
                imported++;
            }
            else
            {
                failed++;
                AddError(errors, rowNumber, Describe(rowResult.Error!));
            }
        }

        Result<Unit> recorded = session.RecordExecution(
            target.Entity.ToString(), table.Name, imported, failed, timeProvider.GetUtcNow());
        if (!recorded.IsSuccess)
        {
            return recorded.Error!;
        }

        await repository.SaveChangesAsync(cancellationToken);

        return new ImportExecutionResponse(
            session.Id,
            session.Status.ToString(),
            table.Rows.Count,
            imported,
            failed,
            errors);
    }

    private static void AddError(List<ImportRowErrorResponse> errors, int rowNumber, string message)
    {
        if (errors.Count < ImportLimits.MaxReportedErrors)
        {
            errors.Add(new ImportRowErrorResponse(rowNumber, message));
        }
    }

    /// <summary>Flattens an inner command's error — including per-field validation failures — into one line.</summary>
    private static string Describe(Error error)
    {
        if (error.Failures is null || error.Failures.Count == 0)
        {
            return error.Message;
        }

        IEnumerable<string> details = error.Failures
            .OrderBy(f => f.Key, StringComparer.Ordinal)
            .Select(f => $"{f.Key}: {string.Join(" ", f.Value)}");
        return string.Join(" ", details);
    }
}
