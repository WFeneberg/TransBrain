using System.Diagnostics.CodeAnalysis;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Features.Imports.CreateImportSession;

internal sealed class CreateApiImportSessionCommandHandler(
    IImportSourceParser parser,
    IImportSessionRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<CreateApiImportSessionCommand, ImportSessionResponse>
{
    public async Task<Result<ImportSessionResponse>> Handle(
        CreateApiImportSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryCreateHttpUrl(command.Url, out Uri? url))
        {
            return Error.Validation(
                "Import.InvalidApiUrl", "The URL must be an absolute http:// or https:// address.");
        }

        Result<ImportTableSet> tables = await parser.ReadApiAsync(url, command.Headers, cancellationToken);
        if (!tables.IsSuccess)
        {
            return tables.Error!;
        }

        if (tables.Value.Tables.Count == 0)
        {
            return Error.Validation("Import.NoTables", "No importable table was found in the API response.");
        }

        Result<ImportSession> session = ImportSession.Create(
            ImportSourceType.RestApi,
            BuildDisplayName(url),
            currentUser.UserName,
            timeProvider.GetUtcNow());
        if (!session.IsSuccess)
        {
            return session.Error!;
        }

        await repository.AddAsync(session.Value, tables.Value, cancellationToken);

        return ImportSessionResponse.From(session.Value, tables.Value);
    }

    internal static bool TryCreateHttpUrl(string url, [NotNullWhen(true)] out Uri? result)
        => Uri.TryCreate(url?.Trim(), UriKind.Absolute, out result)
           && (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// A display name that never leaks credentials: user info, query string (where API keys
    /// often travel) and fragment are dropped; only scheme, host, port and path survive.
    /// </summary>
    internal static string BuildDisplayName(Uri url)
        => url.GetComponents(
            UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path,
            UriFormat.Unescaped);
}
