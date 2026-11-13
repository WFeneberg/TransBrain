using System.Data;
using System.Data.Common;
using System.Globalization;
using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Common;

namespace TransBrain.Infrastructure.Imports;

/// <summary>How a concrete database engine lists its user tables and quotes identifiers.</summary>
internal sealed record DatabaseDialect(
    string DisplayName,
    Func<DbConnection, CancellationToken, Task<IReadOnlyList<string>>> ListTablesAsync,
    Func<string, string> QuoteIdentifier);

/// <summary>
/// Reads every user table of an open ADO.NET connection into the normalized import model. All
/// engine differences are confined to the <see cref="DatabaseDialect"/>; failures become
/// <see cref="Error"/>s so an unreachable database answers like any other bad import source.
/// </summary>
internal static class DatabaseTableReader
{
    private const int CommandTimeoutSeconds = 60;

    public static async Task<Result<ImportTableSet>> ReadAsync(
        DbConnection connection,
        DatabaseDialect dialect,
        CancellationToken cancellationToken)
    {
        try
        {
            await using (connection)
            {
                await connection.OpenAsync(cancellationToken);

                IReadOnlyList<string> tableNames = await dialect.ListTablesAsync(connection, cancellationToken);

                var tables = new List<ImportTable>();
                foreach (string tableName in tableNames.Take(ImportLimits.MaxTables))
                {
                    ImportTable? table = await ReadTableAsync(connection, dialect, tableName, cancellationToken);
                    if (table is not null)
                    {
                        tables.Add(table);
                    }
                }

                return new ImportTableSet(tables);
            }
        }
        catch (Exception exception) when (exception is DbException or InvalidOperationException
                                              or ArgumentException or TimeoutException or IOException)
        {
            return Error.Unavailable(
                "Import.DatabaseUnreachable",
                $"The {dialect.DisplayName} source could not be read: {exception.Message}");
        }
    }

    private static async Task<ImportTable?> ReadTableAsync(
        DbConnection connection,
        DatabaseDialect dialect,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM {QuoteQualified(dialect, tableName)}";
        command.CommandTimeout = CommandTimeoutSeconds;

        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        int fieldCount = Math.Min(reader.FieldCount, ImportLimits.MaxColumns);
        if (fieldCount == 0)
        {
            return null;
        }

        IReadOnlyList<string> columns = ImportParsing.NormalizeColumns(
            Enumerable.Range(0, fieldCount).Select(reader.GetName));

        var rows = new List<IReadOnlyList<string?>>();
        while (rows.Count < ImportLimits.MaxRowsPerTable && await reader.ReadAsync(cancellationToken))
        {
            var row = new string?[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                row[i] = ImportParsing.Truncate(FormatValue(reader, i));
            }

            rows.Add(row);
        }

        return new ImportTable(tableName, columns, rows);
    }

    /// <summary>Quotes "schema.table" part by part; a bare table name is quoted as one identifier.</summary>
    private static string QuoteQualified(DatabaseDialect dialect, string tableName)
    {
        int separator = tableName.IndexOf('.');
        if (separator <= 0 || separator == tableName.Length - 1)
        {
            return dialect.QuoteIdentifier(tableName);
        }

        string schema = tableName[..separator];
        string name = tableName[(separator + 1)..];
        return $"{dialect.QuoteIdentifier(schema)}.{dialect.QuoteIdentifier(name)}";
    }

    private static string? FormatValue(DbDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        object value = reader.GetValue(ordinal);
        return value switch
        {
            string text => text,
            bool flag => flag ? "true" : "false",
            DateTime dateTime => dateTime.TimeOfDay == TimeSpan.Zero
                ? dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : dateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:sszzz", CultureInfo.InvariantCulture),
            DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            TimeOnly time => time.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            TimeSpan span => span.ToString(),
            byte[] => null, // Binary blobs have no tabular representation.
            Guid guid => guid.ToString(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString()
        };
    }

    public static async Task<IReadOnlyList<string>> ListTablesBySqlAsync(
        DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = CommandTimeoutSeconds;

        var names = new List<string>();
        await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                names.Add(reader.GetString(0));
            }
        }

        return names;
    }

    /// <summary>Lists user tables via the provider's ADO.NET schema API (used where no information_schema exists).</summary>
    public static Task<IReadOnlyList<string>> ListTablesBySchemaAsync(
        DbConnection connection, CancellationToken cancellationToken)
    {
        DataTable schema = connection.GetSchema("Tables");

        int nameColumn = schema.Columns.IndexOf("TABLE_NAME");
        int typeColumn = schema.Columns.IndexOf("TABLE_TYPE");

        var names = new List<string>();
        if (nameColumn < 0)
        {
            return Task.FromResult<IReadOnlyList<string>>(names);
        }

        foreach (DataRow row in schema.Rows)
        {
            string? name = row[nameColumn] as string;
            string type = typeColumn >= 0 ? row[typeColumn] as string ?? string.Empty : "TABLE";

            // Keep plain tables; skip views and the engine's own system tables (Access: MSys*).
            if (!string.IsNullOrWhiteSpace(name)
                && type.Contains("TABLE", StringComparison.OrdinalIgnoreCase)
                && !type.Contains("SYSTEM", StringComparison.OrdinalIgnoreCase)
                && !name.StartsWith("MSys", StringComparison.OrdinalIgnoreCase))
            {
                names.Add(name);
            }
        }

        return Task.FromResult<IReadOnlyList<string>>(names);
    }
}
