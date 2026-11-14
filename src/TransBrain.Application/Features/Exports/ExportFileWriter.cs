using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using TransBrain.Application.Features.Imports;

namespace TransBrain.Application.Features.Exports;

/// <summary>
/// Serializes export rows into the download formats. The vocabulary intentionally mirrors the
/// import: CSV and Excel use the import fields' German labels as headers and JSON uses the field
/// names as property names, and values are written in formats <see cref="ImportValueParser"/>
/// accepts — so an exported file can be imported again (here or in a second TransBrain) with
/// every column auto-matched.
/// </summary>
internal static class ExportFileWriter
{
    private static readonly XNamespace Spreadsheet =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly XNamespace Relationships =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static (string ContentType, string Extension, byte[] Content) Write(
        ExportFormat format,
        string sheetName,
        IReadOnlyList<ImportTargetField> fields,
        IReadOnlyList<object?[]> rows) => format switch
    {
        ExportFormat.Csv => ("text/csv", "csv", WriteCsv(fields, rows)),
        ExportFormat.Json => ("application/json", "json", WriteJson(fields, rows)),
        ExportFormat.Xlsx => (
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "xlsx",
            WriteXlsx(sheetName, fields, rows)),
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown export format.")
    };

    /// <summary>Semicolon-separated, CRLF, UTF-8 with BOM — what German Excel expects.</summary>
    private static byte[] WriteCsv(IReadOnlyList<ImportTargetField> fields, IReadOnlyList<object?[]> rows)
    {
        var builder = new StringBuilder();
        builder.AppendJoin(';', fields.Select(f => EscapeCsv(f.Label))).Append("\r\n");

        foreach (object?[] row in rows)
        {
            builder.AppendJoin(';', row.Select(cell => EscapeCsv(ImportValueParser.Format(cell)))).Append("\r\n");
        }

        byte[] text = Encoding.UTF8.GetBytes(builder.ToString());
        byte[] preamble = Encoding.UTF8.GetPreamble();
        byte[] content = new byte[preamble.Length + text.Length];
        preamble.CopyTo(content, 0);
        text.CopyTo(content, preamble.Length);
        return content;
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    /// <summary>An array of flat objects; numbers stay numbers, everything else exports as text.</summary>
    private static byte[] WriteJson(IReadOnlyList<ImportTargetField> fields, IReadOnlyList<object?[]> rows)
    {
        var array = new JsonArray();
        foreach (object?[] row in rows)
        {
            var item = new JsonObject();
            for (int i = 0; i < fields.Count; i++)
            {
                item[fields[i].Name] = row[i] switch
                {
                    null => null,
                    int number => JsonValue.Create(number),
                    decimal number => JsonValue.Create(number),
                    bool flag => JsonValue.Create(flag),
                    var value => JsonValue.Create(ImportValueParser.Format(value))
                };
            }

            array.Add(item);
        }

        return JsonSerializer.SerializeToUtf8Bytes(array, JsonOptions);
    }

    /// <summary>
    /// A minimal single-sheet workbook: numbers as numeric cells, everything else as inline
    /// strings (dates in ISO form) — nothing the import parsers or Excel would miss.
    /// </summary>
    private static byte[] WriteXlsx(
        string sheetName, IReadOnlyList<ImportTargetField> fields, IReadOnlyList<object?[]> rows)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            XNamespace contentTypes = "http://schemas.openxmlformats.org/package/2006/content-types";
            WriteEntry(archive, "[Content_Types].xml", new XDocument(
                new XElement(contentTypes + "Types",
                    new XElement(contentTypes + "Default",
                        new XAttribute("Extension", "rels"),
                        new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(contentTypes + "Default",
                        new XAttribute("Extension", "xml"),
                        new XAttribute("ContentType", "application/xml")),
                    new XElement(contentTypes + "Override",
                        new XAttribute("PartName", "/xl/workbook.xml"),
                        new XAttribute(
                            "ContentType",
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                    new XElement(contentTypes + "Override",
                        new XAttribute("PartName", "/xl/worksheets/sheet1.xml"),
                        new XAttribute(
                            "ContentType",
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));

            XNamespace packageRels = "http://schemas.openxmlformats.org/package/2006/relationships";
            WriteEntry(archive, "_rels/.rels", new XDocument(
                new XElement(packageRels + "Relationships",
                    new XElement(packageRels + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute(
                            "Type",
                            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                        new XAttribute("Target", "xl/workbook.xml")))));

            WriteEntry(archive, "xl/workbook.xml", new XDocument(
                new XElement(Spreadsheet + "workbook",
                    new XAttribute(XNamespace.Xmlns + "r", Relationships),
                    new XElement(Spreadsheet + "sheets",
                        new XElement(Spreadsheet + "sheet",
                            new XAttribute("name", sheetName),
                            new XAttribute("sheetId", "1"),
                            new XAttribute(Relationships + "id", "rId1"))))));

            WriteEntry(archive, "xl/_rels/workbook.xml.rels", new XDocument(
                new XElement(packageRels + "Relationships",
                    new XElement(packageRels + "Relationship",
                        new XAttribute("Id", "rId1"),
                        new XAttribute(
                            "Type",
                            "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                        new XAttribute("Target", "worksheets/sheet1.xml")))));

            var sheetData = new XElement(Spreadsheet + "sheetData");
            sheetData.Add(BuildRow(1, fields.Select(f => (object?)f.Label).ToArray()));
            for (int i = 0; i < rows.Count; i++)
            {
                sheetData.Add(BuildRow(i + 2, rows[i]));
            }

            WriteEntry(archive, "xl/worksheets/sheet1.xml", new XDocument(
                new XElement(Spreadsheet + "worksheet", sheetData)));
        }

        return buffer.ToArray();
    }

    private static XElement BuildRow(int rowNumber, object?[] cells)
    {
        var row = new XElement(Spreadsheet + "row", new XAttribute("r", rowNumber));
        for (int i = 0; i < cells.Length; i++)
        {
            string reference = $"{ColumnName(i)}{rowNumber}";
            switch (cells[i])
            {
                case null:
                    break;
                case int number:
                    row.Add(NumberCell(reference, number.ToString(CultureInfo.InvariantCulture)));
                    break;
                case decimal number:
                    row.Add(NumberCell(reference, number.ToString(CultureInfo.InvariantCulture)));
                    break;
                case var value:
                    row.Add(new XElement(Spreadsheet + "c",
                        new XAttribute("r", reference),
                        new XAttribute("t", "inlineStr"),
                        new XElement(Spreadsheet + "is",
                            new XElement(Spreadsheet + "t", ImportValueParser.Format(value)))));
                    break;
            }
        }

        return row;
    }

    private static XElement NumberCell(string reference, string value) =>
        new(Spreadsheet + "c",
            new XAttribute("r", reference),
            new XElement(Spreadsheet + "v", value));

    private static string ColumnName(int index)
    {
        string name = string.Empty;
        for (int remaining = index; remaining >= 0; remaining = remaining / 26 - 1)
        {
            name = (char)('A' + remaining % 26) + name;
        }

        return name;
    }

    private static void WriteEntry(ZipArchive archive, string path, XDocument document)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        document.Save(stream);
    }
}
