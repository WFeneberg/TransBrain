using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;

namespace TransBrain.Application.Abstractions;

/// <summary>The SQL engines a live-connection import can read from.</summary>
public enum DatabaseImportProvider
{
    PostgreSql,
    SqlServer,

    /// <summary>MySQL and MariaDB.</summary>
    MySql,

    /// <summary>Any source with an installed ODBC driver — the escape hatch for everything else.</summary>
    Odbc
}

/// <summary>
/// Turns an import source into the normalized <see cref="ImportTableSet"/>. Implemented in
/// Infrastructure; failures come back as <see cref="Error"/>s (unreadable file, unreachable
/// database, missing driver), never as exceptions.
/// </summary>
public interface IImportSourceParser
{
    /// <param name="sourceType">A file-based source type; <see cref="ImportSourceType.Database"/> is not valid here.</param>
    Task<Result<ImportTableSet>> ParseFileAsync(
        ImportSourceType sourceType,
        string fileName,
        byte[] content,
        CancellationToken cancellationToken);

    Task<Result<ImportTableSet>> ReadDatabaseAsync(
        DatabaseImportProvider provider,
        string connectionString,
        CancellationToken cancellationToken);
}
