using System.Data.Common;
using System.Data.Odbc;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using TransBrain.Application.Abstractions;

namespace TransBrain.Infrastructure.Imports;

/// <summary>The connection factories and dialects of the supported live-database import sources.</summary>
internal static class DatabaseDialects
{
    public static (DbConnection Connection, DatabaseDialect Dialect) Create(
        DatabaseImportProvider provider, string connectionString) => provider switch
    {
        DatabaseImportProvider.PostgreSql => (new NpgsqlConnection(connectionString), PostgreSql),
        DatabaseImportProvider.SqlServer => (new SqlConnection(connectionString), SqlServer),
        DatabaseImportProvider.MySql => (new MySqlConnection(connectionString), MySql),
        DatabaseImportProvider.Odbc => (new OdbcConnection(connectionString), Odbc),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unknown database import provider.")
    };

    public static DatabaseDialect PostgreSql { get; } = new(
        "PostgreSQL",
        (connection, ct) => DatabaseTableReader.ListTablesBySqlAsync(
            connection,
            """
            SELECT CASE WHEN table_schema = 'public' THEN table_name
                        ELSE table_schema || '.' || table_name END
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE'
              AND table_schema NOT IN ('pg_catalog', 'information_schema')
            ORDER BY 1
            """,
            ct),
        name => $"\"{name.Replace("\"", "\"\"")}\"");

    public static DatabaseDialect SqlServer { get; } = new(
        "SQL Server",
        (connection, ct) => DatabaseTableReader.ListTablesBySqlAsync(
            connection,
            """
            SELECT CASE WHEN TABLE_SCHEMA = 'dbo' THEN TABLE_NAME
                        ELSE TABLE_SCHEMA + '.' + TABLE_NAME END
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_TYPE = 'BASE TABLE'
            ORDER BY 1
            """,
            ct),
        name => $"[{name.Replace("]", "]]")}]");

    public static DatabaseDialect MySql { get; } = new(
        "MySQL/MariaDB",
        (connection, ct) => DatabaseTableReader.ListTablesBySqlAsync(
            connection,
            """
            SELECT table_name FROM information_schema.tables
            WHERE table_type = 'BASE TABLE' AND table_schema = DATABASE()
            ORDER BY 1
            """,
            ct),
        name => $"`{name.Replace("`", "``")}`");

    public static DatabaseDialect Odbc { get; } = new(
        "ODBC",
        DatabaseTableReader.ListTablesBySchemaAsync,
        name => $"\"{name.Replace("\"", "\"\"")}\"");

    /// <summary>Access SQL (both the Windows driver and mdbtools) quotes identifiers with brackets.</summary>
    public static DatabaseDialect Access { get; } = new(
        "Microsoft Access",
        DatabaseTableReader.ListTablesBySchemaAsync,
        name => $"[{name.Replace("]", "]]")}]");

    public static DatabaseDialect Sqlite { get; } = new(
        "SQLite",
        (connection, ct) => DatabaseTableReader.ListTablesBySqlAsync(
            connection,
            """
            SELECT name FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY 1
            """,
            ct),
        name => $"\"{name.Replace("\"", "\"\"")}\"");

    public static SqliteConnection CreateSqliteFileConnection(string filePath) => new(
        new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false // The temp file must be deletable the moment the read is done.
        }.ConnectionString);
}
