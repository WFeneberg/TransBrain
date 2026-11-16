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

/// <summary>One HTTP request header sent along a REST-API import call, for example an Authorization or API-key header.</summary>
public sealed record ImportApiHeader(string Name, string Value);

/// <summary>
/// Turns an import source into the normalized <see cref="ImportTableSet"/>. Implemented in
/// Infrastructure; failures come back as <see cref="Error"/>s (unreadable file, unreachable
/// database, missing driver, unreachable API), never as exceptions.
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

    /// <summary>Fetches a REST API's JSON response once and analyzes it like an uploaded JSON file.</summary>
    Task<Result<ImportTableSet>> ReadApiAsync(
        Uri url,
        IReadOnlyList<ImportApiHeader> headers,
        CancellationToken cancellationToken);
}
