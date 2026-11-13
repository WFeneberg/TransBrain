using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Common;

namespace TransBrain.Infrastructure.Imports;

/// <summary>
/// RFC 4180 style CSV reader with the pragmatics German exports need: the delimiter
/// (<c>;</c>, <c>,</c>, tab or <c>|</c>) is sniffed from the header line, quotes may wrap any
/// field, and quoted fields may contain delimiters, escaped quotes and line breaks.
/// </summary>
internal static class CsvImportParser
{
    private static readonly char[] CandidateDelimiters = [';', ',', '\t', '|'];

    public static Result<ImportTableSet> Parse(string fileName, byte[] content)
    {
        string text = ImportParsing.DecodeText(content);
        if (string.IsNullOrWhiteSpace(text))
        {
            return Error.Validation("Import.EmptyFile", "The file contains no data.");
        }

        char delimiter = SniffDelimiter(text);
        List<List<string?>> records = ReadRecords(text, delimiter);

        // Trailing blank lines produce empty single-cell records; drop them.
        records.RemoveAll(r => r.Count == 1 && string.IsNullOrEmpty(r[0]));

        if (records.Count < 2)
        {
            return Error.Validation(
                "Import.NoDataRows",
                "The file needs a header line and at least one data row.");
        }

        IReadOnlyList<string> columns = ImportParsing.NormalizeColumns(records[0]);
        if (columns.Count > ImportLimits.MaxColumns)
        {
            return Error.Validation(
                "Import.TooManyColumns", $"The file has more than {ImportLimits.MaxColumns} columns.");
        }

        List<IReadOnlyList<string?>> rows = records
            .Skip(1)
            .Take(ImportLimits.MaxRowsPerTable)
            .Select(r => ImportParsing.FitRow(
                r.Select(c => ImportParsing.Truncate(c)).ToList(), columns.Count))
            .ToList();

        string tableName = Path.GetFileNameWithoutExtension(fileName);
        return new ImportTableSet([
            new ImportTable(string.IsNullOrWhiteSpace(tableName) ? "data" : tableName, columns, rows)
        ]);
    }

    /// <summary>The delimiter that splits the header into the most fields wins; ties favour the semicolon.</summary>
    private static char SniffDelimiter(string text)
    {
        string firstLine = text[..(text.IndexOfAny(['\r', '\n']) is var i and >= 0 ? i : text.Length)];

        char best = ';';
        int bestCount = -1;
        foreach (char candidate in CandidateDelimiters)
        {
            int count = CountOutsideQuotes(firstLine, candidate);
            if (count > bestCount)
            {
                best = candidate;
                bestCount = count;
            }
        }

        return best;
    }

    private static int CountOutsideQuotes(string line, char delimiter)
    {
        int count = 0;
        bool inQuotes = false;
        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                count++;
            }
        }

        return count;
    }

    private static List<List<string?>> ReadRecords(string text, char delimiter)
    {
        var records = new List<List<string?>>();
        var record = new List<string?>();
        var field = new System.Text.StringBuilder();
        bool inQuotes = false;
        bool fieldWasQuoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0 && !fieldWasQuoted:
                    inQuotes = true;
                    fieldWasQuoted = true;
                    break;
                case '\r':
                    break; // CRLF: the \n ends the record.
                case '\n':
                    EndField(record, field, ref fieldWasQuoted);
                    records.Add(record);
                    record = [];
                    break;
                default:
                    if (c == delimiter)
                    {
                        EndField(record, field, ref fieldWasQuoted);
                    }
                    else
                    {
                        field.Append(c);
                    }

                    break;
            }
        }

        if (field.Length > 0 || fieldWasQuoted || record.Count > 0)
        {
            EndField(record, field, ref fieldWasQuoted);
            records.Add(record);
        }

        return records;
    }

    private static void EndField(List<string?> record, System.Text.StringBuilder field, ref bool fieldWasQuoted)
    {
        string value = field.ToString();
        record.Add(value.Length == 0 && !fieldWasQuoted ? null : value);
        field.Clear();
        fieldWasQuoted = false;
    }
}
