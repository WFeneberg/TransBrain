using System.Xml;
using System.Xml.Linq;
using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Common;

namespace TransBrain.Infrastructure.Imports;

/// <summary>
/// Reads XML exports by treating repeated "record" elements as rows: every element whose children
/// are all scalar becomes a row candidate, grouped into one table per element name. Attributes
/// and scalar child elements become columns.
/// </summary>
internal static class XmlImportParser
{
    public static Result<ImportTableSet> Parse(byte[] content)
    {
        XDocument document;
        try
        {
            using var stream = new MemoryStream(content);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersFromEntities = 1024
            });
            document = XDocument.Load(reader);
        }
        catch (XmlException exception)
        {
            return Error.Validation("Import.InvalidXml", $"The file is not valid XML: {exception.Message}");
        }

        if (document.Root is null)
        {
            return Error.Validation("Import.InvalidXml", "The XML document has no root element.");
        }

        // Group record candidates (elements with only scalar content) by element name,
        // in document order of first appearance.
        var groups = new Dictionary<string, List<XElement>>(StringComparer.OrdinalIgnoreCase);
        var groupOrder = new List<string>();

        foreach (XElement element in document.Root.DescendantsAndSelf())
        {
            if (!IsRecordCandidate(element))
            {
                continue;
            }

            string name = element.Name.LocalName;
            if (!groups.TryGetValue(name, out List<XElement>? list))
            {
                list = [];
                groups[name] = list;
                groupOrder.Add(name);
            }

            if (list.Count < ImportLimits.MaxRowsPerTable)
            {
                list.Add(element);
            }
        }

        // Only repeated elements form tables — a single <settings> element is configuration,
        // not data. When nothing repeats, fall back to single-record groups so a one-row
        // export still imports.
        List<string> tableNames = groupOrder.Where(n => groups[n].Count > 1).ToList();
        if (tableNames.Count == 0)
        {
            tableNames = groupOrder;
        }

        var tables = new List<ImportTable>();
        foreach (string name in tableNames.Take(ImportLimits.MaxTables))
        {
            ImportTable? table = BuildTable(name, groups[name]);
            if (table is not null)
            {
                tables.Add(table);
            }
        }

        if (tables.Count == 0)
        {
            return Error.Validation(
                "Import.InvalidXmlShape", "No repeated record elements were found in the XML document.");
        }

        return new ImportTableSet(tables);
    }

    /// <summary>A record has at least one attribute or scalar child, and no nested element children.</summary>
    private static bool IsRecordCandidate(XElement element)
    {
        foreach (XElement child in element.Elements())
        {
            if (child.HasElements)
            {
                return false;
            }
        }

        return element.HasElements || element.HasAttributes;
    }

    private static ImportTable? BuildTable(string name, List<XElement> elements)
    {
        var columns = new List<string>();
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<IReadOnlyList<string?>>();

        foreach (XElement element in elements)
        {
            var row = new Dictionary<int, string?>();

            foreach (XAttribute attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration)
                {
                    continue;
                }

                SetCell(columns, columnIndex, row, $"@{attribute.Name.LocalName}", attribute.Value);
            }

            foreach (XElement child in element.Elements())
            {
                SetCell(columns, columnIndex, row, child.Name.LocalName, child.Value);
            }

            var materialized = new string?[ImportLimits.MaxColumns];
            foreach ((int index, string? value) in row)
            {
                materialized[index] = value;
            }

            rows.Add(materialized);
        }

        if (columns.Count == 0)
        {
            return null;
        }

        // Rows were materialized against the final column budget; trim them to the real width.
        List<IReadOnlyList<string?>> fitted = rows
            .Select(r => ImportParsing.FitRow(r, columns.Count))
            .ToList();

        return new ImportTable(name, columns, fitted);
    }

    private static void SetCell(
        List<string> columns,
        Dictionary<string, int> columnIndex,
        Dictionary<int, string?> row,
        string column,
        string value)
    {
        if (!columnIndex.TryGetValue(column, out int index))
        {
            if (columns.Count >= ImportLimits.MaxColumns)
            {
                return;
            }

            index = columns.Count;
            columns.Add(column);
            columnIndex[column] = index;
        }

        row[index] = ImportParsing.Truncate(string.IsNullOrEmpty(value) ? null : value);
    }
}
