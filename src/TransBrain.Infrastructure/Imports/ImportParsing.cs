using System.Text;
using TransBrain.Application.Features.Imports;

namespace TransBrain.Infrastructure.Imports;

/// <summary>Shared helpers for turning raw parser output into well-formed <see cref="ImportTable"/>s.</summary>
internal static class ImportParsing
{
    /// <summary>
    /// Decodes file bytes honouring a BOM, trying strict UTF-8 next, and falling back to
    /// Latin-1 — the encoding legacy German exports actually use when they are not UTF-8.
    /// </summary>
    public static string DecodeText(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var bomReader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        bomReader.Peek();
        if (!Equals(bomReader.CurrentEncoding, Encoding.UTF8))
        {
            return bomReader.ReadToEnd();
        }

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(StripUtf8Bom(content));
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(content);
        }
    }

    private static byte[] StripUtf8Bom(byte[] content)
        => content is [0xEF, 0xBB, 0xBF, ..] ? content[3..] : content;

    /// <summary>Makes header names usable as columns: non-empty, unique, trimmed, length-capped.</summary>
    public static IReadOnlyList<string> NormalizeColumns(IEnumerable<string?> headers)
    {
        var columns = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string? header in headers)
        {
            string name = string.IsNullOrWhiteSpace(header)
                ? $"Column {columns.Count + 1}"
                : Truncate(header.Trim(), 200)!;

            string unique = name;
            for (int suffix = 2; !seen.Add(unique); suffix++)
            {
                unique = $"{name}_{suffix}";
            }

            columns.Add(unique);
        }

        return columns;
    }

    public static string? Truncate(string? value, int maxLength = ImportLimits.MaxCellChars)
        => value is null || value.Length <= maxLength ? value : value[..maxLength];

    /// <summary>Pads or trims a row to the table's column count.</summary>
    public static IReadOnlyList<string?> FitRow(IReadOnlyList<string?> row, int columnCount)
    {
        if (row.Count == columnCount)
        {
            return row;
        }

        var fitted = new string?[columnCount];
        for (int i = 0; i < columnCount && i < row.Count; i++)
        {
            fitted[i] = row[i];
        }

        return fitted;
    }
}
