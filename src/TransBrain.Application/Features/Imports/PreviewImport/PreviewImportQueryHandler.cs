using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Imports.PreviewImport;

internal sealed class PreviewImportQueryHandler(IImportSessionRepository repository)
    : IQueryHandler<PreviewImportQuery, ImportPreviewResponse>
{
    public async Task<Result<ImportPreviewResponse>> Handle(
        PreviewImportQuery query,
        CancellationToken cancellationToken)
    {
        Result<ResolvedImportRequest> resolved = await ImportRequestResolver.ResolveAsync(
            repository, query.SessionId, query.TableName, query.TargetEntity, query.Mappings, cancellationToken);
        if (!resolved.IsSuccess)
        {
            return resolved.Error!;
        }

        (_, ImportTable table, _, IReadOnlyList<ResolvedFieldMapping> mappings) = resolved.Value;

        var rows = new List<ImportPreviewRowResponse>(Math.Min(query.MaxRows, table.Rows.Count));
        int valid = 0;
        int invalid = 0;

        for (int i = 0; i < table.Rows.Count; i++)
        {
            ImportRowConversion conversion = ImportRowConverter.Convert(mappings, table.Rows[i]);
            if (conversion.IsValid)
            {
                valid++;
            }
            else
            {
                invalid++;
            }

            if (rows.Count < query.MaxRows)
            {
                rows.Add(new ImportPreviewRowResponse(i + 1, conversion.DisplayValues, conversion.Errors));
            }
        }

        return new ImportPreviewResponse(table.Rows.Count, valid, invalid, rows);
    }
}
