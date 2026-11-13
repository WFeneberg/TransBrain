namespace TransBrain.Domain.Imports;

/// <summary>Where an import session's data came from.</summary>
public enum ImportSourceType
{
    /// <summary>A delimited text file (.csv, .txt); the delimiter is sniffed from the content.</summary>
    Csv,

    /// <summary>A JSON file (.json): a root array of objects, or an object whose array properties become tables.</summary>
    Json,

    /// <summary>An XML file (.xml); repeated elements become rows.</summary>
    Xml,

    /// <summary>An Excel workbook (.xlsx); each worksheet becomes a table.</summary>
    Excel,

    /// <summary>A Microsoft Access database file (.mdb, .accdb), read through the Access ODBC driver.</summary>
    Access,

    /// <summary>A SQLite database file (.sqlite, .db, .sqlite3).</summary>
    Sqlite,

    /// <summary>A live SQL database reached over a connection string (PostgreSQL, SQL Server, MySQL/MariaDB, ODBC).</summary>
    Database
}
