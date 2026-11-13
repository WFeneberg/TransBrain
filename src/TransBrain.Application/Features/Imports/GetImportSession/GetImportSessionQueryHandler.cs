using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Features.Imports.GetImportSession;

internal sealed class GetImportSessionQueryHandler(IImportSessionRepository repository)
    : IQueryHandler<GetImportSessionQuery, ImportSessionResponse>
{
    public async Task<Result<ImportSessionResponse>> Handle(
        GetImportSessionQuery query,
        CancellationToken cancellationToken)
    {
        ImportSession? session = await repository.GetByIdAsync(query.Id, cancellationToken);
        if (session is null)
        {
            return Error.NotFound("Import.SessionNotFound", $"Import session '{query.Id}' was not found.");
        }

        ImportTableSet? tables = await repository.GetTableSetAsync(query.Id, cancellationToken);

        return ImportSessionResponse.From(session, tables);
    }
}
