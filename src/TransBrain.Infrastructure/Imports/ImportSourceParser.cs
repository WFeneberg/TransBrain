using System.Data.Common;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Infrastructure.Imports;

/// <summary>Routes each import source type to its parser. The Application layer only sees this seam.</summary>
internal sealed class ImportSourceParser : IImportSourceParser
{
    public async Task<Result<ImportTableSet>> ParseFileAsync(
        ImportSourceType sourceType,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        try
        {
            return sourceType switch
            {
                ImportSourceType.Csv => CsvImportParser.Parse(fileName, content),
                ImportSourceType.Json => JsonImportParser.Parse(fileName, content),
                ImportSourceType.Xml => XmlImportParser.Parse(content),
                ImportSourceType.Excel => XlsxImportParser.Parse(content),
                ImportSourceType.Access => await DatabaseFileImportParser.ParseAccessAsync(
                    fileName, content, cancellationToken),
                ImportSourceType.Sqlite => await DatabaseFileImportParser.ParseSqliteAsync(
                    content, cancellationToken),
                _ => Error.Validation(
                    "Import.UnsupportedSourceType",
                    $"'{sourceType}' is not a file-based import source.")
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A malformed file must never 500: whatever slipped past a parser's own handling
            // still comes back as a readable validation error.
            return Error.Validation(
                "Import.UnreadableFile",
                $"The file could not be read as {sourceType}: {exception.Message}");
        }
    }

    public async Task<Result<ImportTableSet>> ReadDatabaseAsync(
        DatabaseImportProvider provider,
        string connectionString,
        CancellationToken cancellationToken)
    {
        DbConnection connection;
        DatabaseDialect dialect;
        try
        {
            (connection, dialect) = DatabaseDialects.Create(provider, connectionString);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            return Error.Validation(
                "Import.InvalidConnectionString", $"The connection string is invalid: {exception.Message}");
        }

        return await DatabaseTableReader.ReadAsync(connection, dialect, cancellationToken);
    }
}
