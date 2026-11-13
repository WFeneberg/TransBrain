using TransBrain.Application.Features.Imports.Targets;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Imports;

/// <summary>A field mapping resolved against a concrete source table: the column index, or -1 for constant-only.</summary>
internal sealed record ResolvedFieldMapping(ImportTargetField Field, int ColumnIndex, string? ConstantValue);

/// <summary>The outcome of converting one source row: typed values, display strings, and what went wrong.</summary>
internal sealed record ImportRowConversion(
    ImportRowValues? Values,
    IReadOnlyDictionary<string, string?> DisplayValues,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// The mapping engine shared by preview and execution: resolves a user-drawn mapping against a
/// source table once, then converts rows cell by cell. Preview and execution therefore can never
/// disagree about what a mapping means.
/// </summary>
internal static class ImportRowConverter
{
    public static Result<IReadOnlyList<ResolvedFieldMapping>> Resolve(
        ImportTable table,
        IImportTargetDefinition target,
        IReadOnlyList<ImportFieldMapping> mappings)
    {
        var resolved = new List<ResolvedFieldMapping>();
        var seenFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ImportFieldMapping mapping in mappings)
        {
            ImportTargetField? field = target.Fields
                .FirstOrDefault(f => string.Equals(f.Name, mapping.TargetField, StringComparison.OrdinalIgnoreCase));
            if (field is null)
            {
                return Error.Validation(
                    "Import.UnknownTargetField",
                    $"'{mapping.TargetField}' is not a field of target '{target.Entity}'.");
            }

            if (!seenFields.Add(field.Name))
            {
                return Error.Validation(
                    "Import.DuplicateTargetField",
                    $"Field '{field.Name}' is mapped more than once.");
            }

            bool hasConstant = !string.IsNullOrWhiteSpace(mapping.ConstantValue);
            if (string.IsNullOrWhiteSpace(mapping.SourceColumn))
            {
                if (!hasConstant)
                {
                    continue; // An empty mapping row from the UI; same as unmapped.
                }

                resolved.Add(new ResolvedFieldMapping(field, ColumnIndex: -1, mapping.ConstantValue!.Trim()));
                continue;
            }

            int columnIndex = IndexOfColumn(table, mapping.SourceColumn);
            if (columnIndex < 0)
            {
                return Error.Validation(
                    "Import.UnknownSourceColumn",
                    $"Column '{mapping.SourceColumn}' does not exist in table '{table.Name}'.");
            }

            resolved.Add(new ResolvedFieldMapping(
                field, columnIndex, hasConstant ? mapping.ConstantValue!.Trim() : null));
        }

        ImportTargetField? unmappedRequired = target.Fields
            .FirstOrDefault(f => f.Required && !seenFields.Contains(f.Name));
        if (unmappedRequired is not null)
        {
            return Error.Validation(
                "Import.RequiredFieldNotMapped",
                $"Required field '{unmappedRequired.Name}' ({unmappedRequired.Label}) is not mapped.");
        }

        return resolved;
    }

    private static int IndexOfColumn(ImportTable table, string sourceColumn)
    {
        for (int i = 0; i < table.Columns.Count; i++)
        {
            if (string.Equals(table.Columns[i], sourceColumn, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    public static ImportRowConversion Convert(
        IReadOnlyList<ResolvedFieldMapping> resolvedMappings,
        IReadOnlyList<string?> row)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var display = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();

        foreach (ResolvedFieldMapping mapping in resolvedMappings)
        {
            string? raw = mapping.ColumnIndex >= 0 && mapping.ColumnIndex < row.Count
                ? row[mapping.ColumnIndex]
                : null;

            // The constant fills empty cells — both for constant-only mappings and as a default.
            if (string.IsNullOrWhiteSpace(raw) && mapping.ConstantValue is not null)
            {
                raw = mapping.ConstantValue;
            }

            Result<object?> parsed = ImportValueParser.Parse(mapping.Field.Kind, raw);
            if (!parsed.IsSuccess)
            {
                errors.Add($"{mapping.Field.Name}: {parsed.Error!.Message}");
                display[mapping.Field.Name] = raw;
                continue;
            }

            if (parsed.Value is null && mapping.Field.Required)
            {
                errors.Add($"{mapping.Field.Name}: value is required but empty.");
                display[mapping.Field.Name] = null;
                continue;
            }

            values[mapping.Field.Name] = parsed.Value;
            display[mapping.Field.Name] = ImportValueParser.Format(parsed.Value);
        }

        return new ImportRowConversion(
            errors.Count == 0 ? new ImportRowValues(values) : null,
            display,
            errors);
    }
}
