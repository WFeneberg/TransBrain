using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Features.Imports.DeleteImportSession;

internal sealed class DeleteImportSessionCommandHandler(IImportSessionRepository repository)
    : ICommandHandler<DeleteImportSessionCommand, Unit>
{
    public async Task<Result<Unit>> Handle(
        DeleteImportSessionCommand command,
        CancellationToken cancellationToken)
    {
        ImportSession? session = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (session is null)
        {
            return Error.NotFound("Import.SessionNotFound", $"Import session '{command.Id}' was not found.");
        }

        await repository.RemoveAsync(session, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
