using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Features.Imports.ListImportSessions;

internal sealed class ListImportSessionsQueryHandler(IImportSessionRepository repository)
    : IQueryHandler<ListImportSessionsQuery, IReadOnlyList<ImportSessionResponse>>
{
    public async Task<Result<IReadOnlyList<ImportSessionResponse>>> Handle(
        ListImportSessionsQuery query,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ImportSession> sessions = await repository.ListAsync(
            ListImportSessionsQuery.MaxSessions, cancellationToken);

        return Result<IReadOnlyList<ImportSessionResponse>>.Success(
            sessions.Select(s => ImportSessionResponse.From(s)).ToList());
    }
}
