using System.Text.Json;
using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Common;

namespace TransBrain.Infrastructure.Imports;

/// <summary>
/// Reads JSON exports: a root array of objects becomes one table; a root object contributes one
/// table per array-of-objects property. Nested objects are flattened to dotted columns
/// ("address.city"), scalar arrays join with "; ".
/// </summary>
internal static class JsonImportParser
{
    public static Result<ImportTableSet> Parse(string fileName, byte[] content)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(content, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
        }
        catch (JsonException exception)
        {
            return Error.Validation("Import.InvalidJson", $"The file is not valid JSON: {exception.Message}");
        }

        using (document)
        {
            var tables = new List<ImportTable>();
            string defaultName = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrWhiteSpace(defaultName))
            {
                defaultName = "data";
            }

            switch (document.RootElement.ValueKind)
            {
                case JsonValueKind.Array:
                    AddTable(tables, defaultName, document.RootElement);
                    break;
                case JsonValueKind.Object:
                    foreach (JsonProperty property in document.RootElement.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.Array && tables.Count < ImportLimits.MaxTables)
                        {
                            AddTable(tables, property.Name, property.Value);
                        }
                    }

                    break;
                default:
                    return Error.Validation(
                        "Import.InvalidJsonShape",
                        "The JSON root must be an array of objects or an object containing arrays.");
            }

            if (tables.Count == 0)
            {
                return Error.Validation(
                    "Import.InvalidJsonShape",
                    "No array of objects was found in the JSON document.");
            }

            return new ImportTableSet(tables);
        }
    }

    private static void AddTable(List<ImportTable> tables, string name, JsonElement array)
    {
        var columns = new List<string>();
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var flatRows = new List<Dictionary<int, string?>>();

        foreach (JsonElement element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (flatRows.Count >= ImportLimits.MaxRowsPerTable)
            {
                break;
            }

            var row = new Dictionary<int, string?>();
            Flatten(element, prefix: null, columns, columnIndex, row);
            flatRows.Add(row);
        }

        if (flatRows.Count == 0 || columns.Count == 0)
        {
            return;
        }

        List<IReadOnlyList<string?>> rows = flatRows
            .Select(flat =>
            {
                var materialized = new string?[columns.Count];
                foreach ((int index, string? value) in flat)
                {
                    materialized[index] = value;
                }

                return (IReadOnlyList<string?>)materialized;
            })
            .ToList();

        tables.Add(new ImportTable(name, columns, rows));
    }

    private static void Flatten(
        JsonElement element,
        string? prefix,
        List<string> columns,
        Dictionary<string, int> columnIndex,
        Dictionary<int, string?> row)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            string path = prefix is null ? property.Name : $"{prefix}.{property.Name}";

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                Flatten(property.Value, path, columns, columnIndex, row);
                continue;
            }

            string? value = ToCellValue(property.Value);
            if (value is null && property.Value.ValueKind == JsonValueKind.Array)
            {
                continue; // Arrays of objects inside a row have no tabular representation.
            }

            if (!columnIndex.TryGetValue(path, out int index))
            {
                if (columns.Count >= ImportLimits.MaxColumns)
                {
                    continue;
                }

                index = columns.Count;
                columns.Add(path);
                columnIndex[path] = index;
            }

            row[index] = ImportParsing.Truncate(value);
        }
    }

    private static string? ToCellValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number => value.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.Array when value.EnumerateArray().All(IsScalar) => string.Join(
            "; ", value.EnumerateArray().Select(ToCellValue).Where(v => v is not null)),
        _ => null
    };

    private static bool IsScalar(JsonElement element) => element.ValueKind is
        JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null;
}
