using System.Data.Odbc;
using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Common;

namespace TransBrain.Infrastructure.Imports;

/// <summary>
/// Reads uploaded database files (Access .mdb/.accdb through an ODBC driver, SQLite directly) by
/// spooling them to a temp file — both engines want a file path, not a stream. The temp file is
/// removed as soon as the read finishes.
/// </summary>
internal static class DatabaseFileImportParser
{
    /// <summary>
    /// The Access ODBC drivers worth probing, most specific first: the Windows driver from
    /// Office/ACE, then mdbtools as packaged on Linux (both spellings exist in the wild).
    /// </summary>
    private static readonly string[] AccessDriverNames =
    [
        "Microsoft Access Driver (*.mdb, *.accdb)",
        "MDBTools",
        "MDBToolsODBC"
    ];

    public static async Task<Result<ImportTableSet>> ParseAccessAsync(
        string fileName, byte[] content, CancellationToken cancellationToken)
    {
        string tempPath = await SpoolAsync(content, Path.GetExtension(fileName), cancellationToken);
        try
        {
            var errors = new List<string>();
            foreach (string driver in AccessDriverNames)
            {
                var connection = new OdbcConnection($"Driver={{{driver}}};Dbq={tempPath};");
                Result<ImportTableSet> result = await DatabaseTableReader.ReadAsync(
                    connection, DatabaseDialects.Access, cancellationToken);

                if (result.IsSuccess)
                {
                    return result;
                }

                errors.Add(result.Error!.Message);
            }

            return Error.Unavailable(
                "Import.AccessDriverMissing",
                "The Access file could not be opened with any installed ODBC driver "
                + "(tried: " + string.Join(", ", AccessDriverNames) + "). "
                + "Install the Microsoft Access Database Engine (Windows) or odbc-mdbtools (Linux). "
                + "Last error: " + errors[^1]);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    public static async Task<Result<ImportTableSet>> ParseSqliteAsync(
        byte[] content, CancellationToken cancellationToken)
    {
        // SQLite databases start with this magic; rejecting other bytes early beats a cryptic driver error.
        if (content.Length < 16 || !"SQLite format 3\0"u8.SequenceEqual(content.AsSpan(0, 16)))
        {
            return Error.Validation("Import.InvalidSqlite", "The file is not a SQLite database.");
        }

        string tempPath = await SpoolAsync(content, ".sqlite", cancellationToken);
        try
        {
            return await DatabaseTableReader.ReadAsync(
                DatabaseDialects.CreateSqliteFileConnection(tempPath),
                DatabaseDialects.Sqlite,
                cancellationToken);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static async Task<string> SpoolAsync(byte[] content, string extension, CancellationToken cancellationToken)
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"transbrain-import-{Guid.NewGuid():N}{(string.IsNullOrEmpty(extension) ? ".tmp" : extension)}");
        await File.WriteAllBytesAsync(path, content, cancellationToken);
        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // A straggling handle (e.g. a driver that unlocks lazily) must not fail the import;
            // the OS temp cleanup collects the file later.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
