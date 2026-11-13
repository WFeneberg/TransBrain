using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Features.Imports.CreateImportSession;

internal sealed class CreateFileImportSessionCommandHandler(
    IImportSourceParser parser,
    IImportSessionRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<CreateFileImportSessionCommand, ImportSessionResponse>
{
    public async Task<Result<ImportSessionResponse>> Handle(
        CreateFileImportSessionCommand command,
        CancellationToken cancellationToken)
    {
        ImportSourceType? sourceType = ImportFileTypes.Resolve(command.FileName);
        if (sourceType is null)
        {
            return Error.Validation(
                "Import.UnsupportedFileType",
                $"Unsupported file type. Supported: {string.Join(", ", ImportFileTypes.SupportedExtensions.Order(StringComparer.Ordinal))}.");
        }

        Result<ImportTableSet> tables = await parser.ParseFileAsync(
            sourceType.Value, command.FileName, command.Content, cancellationToken);
        if (!tables.IsSuccess)
        {
            return tables.Error!;
        }

        if (tables.Value.Tables.Count == 0)
        {
            return Error.Validation("Import.NoTables", "No importable table was found in the source.");
        }

        Result<ImportSession> session = ImportSession.Create(
            sourceType.Value,
            Path.GetFileName(command.FileName),
            currentUser.UserName,
            timeProvider.GetUtcNow());
        if (!session.IsSuccess)
        {
            return session.Error!;
        }

        await repository.AddAsync(session.Value, tables.Value, cancellationToken);

        return ImportSessionResponse.From(session.Value, tables.Value);
    }
}
