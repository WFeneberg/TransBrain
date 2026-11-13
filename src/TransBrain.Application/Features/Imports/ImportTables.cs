namespace TransBrain.Application.Features.Imports;

/// <summary>
/// The normalized shape every import source (file or database) is parsed into: named tables with
/// string cells. Normalizing at analysis time means mapping, preview and execution never touch
/// the original source again — no connection string is kept, no file is re-parsed.
/// </summary>
public sealed record ImportTableSet(IReadOnlyList<ImportTable> Tables);

/// <summary>One table of an analyzed import source. Cell values are raw strings; typing happens at mapping time.</summary>
public sealed record ImportTable(
    string Name,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string?>> Rows);

/// <summary>Hard caps that keep an import session bounded in memory and storage.</summary>
public static class ImportLimits
{
    public const int MaxFileBytes = 25 * 1024 * 1024;

    public const int MaxTables = 100;

    public const int MaxColumns = 256;

    public const int MaxRowsPerTable = 50_000;

    /// <summary>Cells longer than this are truncated by the parsers.</summary>
    public const int MaxCellChars = 4_000;

    /// <summary>Rows shown per table when a session is analyzed.</summary>
    public const int SampleRows = 5;

    /// <summary>Per-row errors reported back from one execution; the counts stay exact beyond this.</summary>
    public const int MaxReportedErrors = 100;
}
