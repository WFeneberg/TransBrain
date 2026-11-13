using System.Data.Common;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Common.Messaging;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Features.Imports.CreateImportSession;

internal sealed class CreateDatabaseImportSessionCommandHandler(
    IImportSourceParser parser,
    IImportSessionRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider)
    : ICommandHandler<CreateDatabaseImportSessionCommand, ImportSessionResponse>
{
    private static readonly string[] HostKeys = ["host", "server", "data source", "dsn"];
    private static readonly string[] DatabaseKeys = ["database", "initial catalog"];

    public async Task<Result<ImportSessionResponse>> Handle(
        CreateDatabaseImportSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse(command.Provider, ignoreCase: true, out DatabaseImportProvider provider))
        {
            return Error.Validation(
                "Import.UnknownProvider",
                $"Unknown provider. Supported: {string.Join(", ", Enum.GetNames<DatabaseImportProvider>())}.");
        }

        Result<ImportTableSet> tables = await parser.ReadDatabaseAsync(
            provider, command.ConnectionString, cancellationToken);
        if (!tables.IsSuccess)
        {
            return tables.Error!;
        }

        if (tables.Value.Tables.Count == 0)
        {
            return Error.Validation("Import.NoTables", "No importable table was found in the source database.");
        }

        Result<ImportSession> session = ImportSession.Create(
            ImportSourceType.Database,
            BuildDisplayName(provider, command.ConnectionString),
            currentUser.UserName,
            timeProvider.GetUtcNow());
        if (!session.IsSuccess)
        {
            return session.Error!;
        }

        await repository.AddAsync(session.Value, tables.Value, cancellationToken);

        return ImportSessionResponse.From(session.Value, tables.Value);
    }

    /// <summary>
    /// A display name that never leaks credentials: only host and database name survive,
    /// everything else of the connection string is dropped.
    /// </summary>
    internal static string BuildDisplayName(DatabaseImportProvider provider, string connectionString)
    {
        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            string? host = FirstValue(builder, HostKeys);
            string? database = FirstValue(builder, DatabaseKeys);

            string details = (host, database) switch
            {
                (not null, not null) => $"{host}/{database}",
                (not null, null) => host,
                (null, not null) => database,
                _ => string.Empty
            };

            return details.Length == 0 ? provider.ToString() : $"{provider}: {details}";
        }
        catch (ArgumentException)
        {
            // Unparseable connection string: the provider alone is still a truthful display name.
            return provider.ToString();
        }
    }

    private static string? FirstValue(DbConnectionStringBuilder builder, string[] keys)
    {
        foreach (string key in keys)
        {
            if (builder.TryGetValue(key, out object? value) && value is string text && text.Length > 0)
            {
                return text;
            }
        }

        return null;
    }
}
