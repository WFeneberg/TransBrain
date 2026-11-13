using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Common;

namespace TransBrain.Infrastructure.Imports;

/// <summary>
/// Minimal .xlsx (SpreadsheetML) reader: one table per worksheet, first non-empty row as header.
/// Hand-rolled over <see cref="ZipArchive"/> on purpose — reading cells, shared strings and
/// date-formatted numbers does not justify a spreadsheet dependency. Dates are resolved through
/// the style table and emitted as ISO strings; everything else arrives as the raw cell text.
/// </summary>
internal static class XlsxImportParser
{
    private static readonly DateTime ExcelEpoch = new(1899, 12, 30);

    // ECMA-376 built-in number formats that render as dates/times.
    private static readonly HashSet<int> BuiltInDateFormats =
    [
        14, 15, 16, 17, 18, 19, 20, 21, 22,
        27, 28, 29, 30, 31, 32, 33, 34, 35, 36,
        45, 46, 47, 50, 51, 52, 53, 54, 55, 56, 57, 58
    ];

    public static Result<ImportTableSet> Parse(byte[] content)
    {
        try
        {
            using var stream = new MemoryStream(content);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            IReadOnlyList<(string Name, string Path)> sheets = ReadSheetCatalog(archive);
            if (sheets.Count == 0)
            {
                return Error.Validation("Import.InvalidXlsx", "The workbook contains no worksheets.");
            }

            IReadOnlyList<string> sharedStrings = ReadSharedStrings(archive);
            IReadOnlySet<int> dateStyles = ReadDateStyleIndexes(archive);

            var tables = new List<ImportTable>();
            foreach ((string name, string path) in sheets.Take(ImportLimits.MaxTables))
            {
                ZipArchiveEntry? entry = archive.GetEntry(path);
                if (entry is null)
                {
                    continue;
                }

                ImportTable? table = ReadSheet(name, entry, sharedStrings, dateStyles);
                if (table is not null)
                {
                    tables.Add(table);
                }
            }

            if (tables.Count == 0)
            {
                return Error.Validation("Import.NoDataRows", "No worksheet contains a header row and data rows.");
            }

            return new ImportTableSet(tables);
        }
        catch (InvalidDataException)
        {
            return Error.Validation("Import.InvalidXlsx", "The file is not a valid .xlsx workbook.");
        }
        catch (XmlException exception)
        {
            return Error.Validation("Import.InvalidXlsx", $"The workbook contains invalid XML: {exception.Message}");
        }
    }

    private static IReadOnlyList<(string Name, string Path)> ReadSheetCatalog(ZipArchive archive)
    {
        XDocument? workbook = LoadXml(archive, "xl/workbook.xml");
        XDocument? rels = LoadXml(archive, "xl/_rels/workbook.xml.rels");
        if (workbook is null || rels is null)
        {
            return [];
        }

        Dictionary<string, string> targets = rels.Root!
            .Elements().Where(e => e.Name.LocalName == "Relationship")
            .ToDictionary(
                e => (string?)e.Attribute("Id") ?? string.Empty,
                e => NormalizeTarget((string?)e.Attribute("Target") ?? string.Empty));

        var sheets = new List<(string, string)>();
        foreach (XElement sheet in workbook.Descendants().Where(e => e.Name.LocalName == "sheet"))
        {
            string? relationId = sheet.Attributes()
                .FirstOrDefault(a => a.Name.LocalName == "id" && a.Name.Namespace != XNamespace.None)?.Value;
            string name = (string?)sheet.Attribute("name") ?? $"Sheet {sheets.Count + 1}";

            if (relationId is not null && targets.TryGetValue(relationId, out string? target))
            {
                sheets.Add((name, target));
            }
        }

        return sheets;
    }

    private static string NormalizeTarget(string target)
        => target.StartsWith('/') ? target.TrimStart('/') : $"xl/{target}";

    private static IReadOnlyList<string> ReadSharedStrings(ZipArchive archive)
    {
        XDocument? document = LoadXml(archive, "xl/sharedStrings.xml");
        if (document?.Root is null)
        {
            return [];
        }

        // One entry per <si>; rich-text runs (<r><t>…</t></r>) concatenate to one string.
        return document.Root
            .Elements().Where(e => e.Name.LocalName == "si")
            .Select(si => string.Concat(
                si.Descendants().Where(e => e.Name.LocalName == "t").Select(t => t.Value)))
            .ToList();
    }

    /// <summary>The cell-format indexes (s="...") whose number format renders as a date or time.</summary>
    private static IReadOnlySet<int> ReadDateStyleIndexes(ZipArchive archive)
    {
        XDocument? styles = LoadXml(archive, "xl/styles.xml");
        if (styles is null)
        {
            return new HashSet<int>();
        }

        var dateFormatIds = new HashSet<int>(BuiltInDateFormats);
        foreach (XElement format in styles.Descendants().Where(e => e.Name.LocalName == "numFmt"))
        {
            if (int.TryParse((string?)format.Attribute("numFmtId"), out int id)
                && IsDateFormatCode((string?)format.Attribute("formatCode") ?? string.Empty))
            {
                dateFormatIds.Add(id);
            }
        }

        var dateStyles = new HashSet<int>();
        XElement? cellFormats = styles.Descendants().FirstOrDefault(e => e.Name.LocalName == "cellXfs");
        if (cellFormats is null)
        {
            return dateStyles;
        }

        int index = 0;
        foreach (XElement xf in cellFormats.Elements().Where(e => e.Name.LocalName == "xf"))
        {
            if (int.TryParse((string?)xf.Attribute("numFmtId"), out int formatId) && dateFormatIds.Contains(formatId))
            {
                dateStyles.Add(index);
            }

            index++;
        }

        return dateStyles;
    }

    /// <summary>Date/time format codes contain y/d/h or a standalone m outside quoted literals.</summary>
    private static bool IsDateFormatCode(string code)
    {
        bool inQuotes = false;
        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            switch (c)
            {
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case '[': // Skip color/locale blocks like [Red] or [$-407].
                    while (i < code.Length && code[i] != ']')
                    {
                        i++;
                    }

                    break;
                case '\\':
                    i++; // Escaped literal character.
                    break;
                default:
                    if (!inQuotes && c is 'y' or 'Y' or 'd' or 'D' or 'h' or 'H' or 'm' or 'M' or 's' or 'S')
                    {
                        return true;
                    }

                    break;
            }
        }

        return false;
    }

    private static ImportTable? ReadSheet(
        string name,
        ZipArchiveEntry entry,
        IReadOnlyList<string> sharedStrings,
        IReadOnlySet<int> dateStyles)
    {
        var rows = new List<Dictionary<int, string?>>();

        using (Stream stream = entry.Open())
        using (XmlReader reader = CreateReader(stream))
        {
            Dictionary<int, string?>? row = null;
            int nextColumn = 0;
            int currentColumn = 0;
            string? cellType = null;
            int cellStyle = -1;
            bool inValue = false;
            bool inInlineString = false;
            var cellText = new System.Text.StringBuilder();
            bool cellHasValue = false;

            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element when reader.LocalName == "row":
                        row = [];
                        nextColumn = 0;
                        break;

                    case XmlNodeType.Element when reader.LocalName == "c" && row is not null:
                        currentColumn = ParseColumnIndex(reader.GetAttribute("r")) ?? nextColumn;
                        nextColumn = currentColumn + 1;
                        cellType = reader.GetAttribute("t");
                        cellStyle = int.TryParse(reader.GetAttribute("s"), out int s) ? s : -1;
                        cellText.Clear();
                        cellHasValue = false;
                        if (reader.IsEmptyElement)
                        {
                            cellType = null;
                        }

                        break;

                    case XmlNodeType.Element when reader.LocalName == "is":
                        inInlineString = true;
                        break;

                    case XmlNodeType.EndElement when reader.LocalName == "is":
                        inInlineString = false;
                        break;

                    case XmlNodeType.Element when reader.LocalName == "v"
                                                  || (reader.LocalName == "t" && inInlineString):
                        if (!reader.IsEmptyElement)
                        {
                            inValue = true;
                        }

                        cellHasValue = true;
                        break;

                    case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace when inValue:
                        cellText.Append(reader.Value);
                        break;

                    case XmlNodeType.EndElement when reader.LocalName is "v" or "t":
                        inValue = false;
                        break;

                    case XmlNodeType.EndElement when reader.LocalName == "c" && row is not null:
                        if (cellHasValue && currentColumn < ImportLimits.MaxColumns)
                        {
                            row[currentColumn] = ResolveCellValue(
                                cellText.ToString(), cellType, cellStyle, sharedStrings, dateStyles);
                        }

                        break;

                    case XmlNodeType.EndElement when reader.LocalName == "row" && row is not null:
                        if (row.Count > 0)
                        {
                            rows.Add(row);
                        }

                        row = null;
                        break;
                }

                // Header + capped data rows are all we need; stop streaming beyond that.
                if (rows.Count > ImportLimits.MaxRowsPerTable)
                {
                    break;
                }
            }
        }

        if (rows.Count < 2)
        {
            return null; // No header plus data.
        }

        Dictionary<int, string?> headerRow = rows[0];
        int columnCount = Math.Min(headerRow.Keys.Max() + 1, ImportLimits.MaxColumns);
        IReadOnlyList<string> columns = ImportParsing.NormalizeColumns(
            Enumerable.Range(0, columnCount).Select(i => headerRow.GetValueOrDefault(i)));

        List<IReadOnlyList<string?>> dataRows = rows
            .Skip(1)
            .Take(ImportLimits.MaxRowsPerTable)
            .Select(sparse =>
            {
                var materialized = new string?[columnCount];
                foreach ((int column, string? value) in sparse)
                {
                    if (column < columnCount)
                    {
                        materialized[column] = ImportParsing.Truncate(value);
                    }
                }

                return (IReadOnlyList<string?>)materialized;
            })
            .ToList();

        return new ImportTable(name, columns, dataRows);
    }

    private static string? ResolveCellValue(
        string rawText,
        string? cellType,
        int cellStyle,
        IReadOnlyList<string> sharedStrings,
        IReadOnlySet<int> dateStyles)
    {
        switch (cellType)
        {
            case "s":
                return int.TryParse(rawText, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                       && index >= 0 && index < sharedStrings.Count
                    ? NullIfEmpty(sharedStrings[index])
                    : null;
            case "str" or "inlineStr":
                return NullIfEmpty(rawText);
            case "b":
                return rawText.Trim() == "1" ? "true" : "false";
            case "e":
                return null;
            case "d":
                return NullIfEmpty(rawText); // ISO 8601 text; the value parser understands it.
            default:
                return ResolveNumeric(rawText, cellStyle, dateStyles);
        }
    }

    private static string? ResolveNumeric(string rawText, int cellStyle, IReadOnlySet<int> dateStyles)
    {
        string trimmed = rawText.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (!dateStyles.Contains(cellStyle)
            || !double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double serial)
            || serial is < 0 or > 2_958_465)
        {
            return trimmed;
        }

        // A date-styled serial: render as ISO so the mapping's date parser takes it from here.
        DateTime value = ExcelEpoch.AddDays(serial);
        return serial < 1
            ? trimmed // Pure time-of-day; there is no date to extract.
            : value.TimeOfDay == TimeSpan.Zero
                ? value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : value.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

    /// <summary>"BC12" → zero-based column 54; null when the reference is missing.</summary>
    private static int? ParseColumnIndex(string? cellReference)
    {
        if (string.IsNullOrEmpty(cellReference))
        {
            return null;
        }

        int column = 0;
        bool hasLetters = false;
        foreach (char c in cellReference)
        {
            if (char.IsAsciiLetterUpper(c))
            {
                column = column * 26 + (c - 'A' + 1);
                hasLetters = true;
            }
            else if (char.IsAsciiLetterLower(c))
            {
                column = column * 26 + (c - 'a' + 1);
                hasLetters = true;
            }
            else
            {
                break;
            }
        }

        return hasLetters ? column - 1 : null;
    }

    private static XDocument? LoadXml(ZipArchive archive, string path)
    {
        ZipArchiveEntry? entry = archive.GetEntry(path);
        if (entry is null)
        {
            return null;
        }

        using Stream stream = entry.Open();
        using XmlReader reader = CreateReader(stream);
        return XDocument.Load(reader);
    }

    private static XmlReader CreateReader(Stream stream) => XmlReader.Create(stream, new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null
    });
}
