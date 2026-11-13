namespace TransBrain.Application.Features.Imports;

/// <summary>Typed accessors over one converted row, keyed by target field name.</summary>
public sealed class ImportRowValues(IReadOnlyDictionary<string, object?> values)
{
    public string? GetText(string field) => Get(field) as string;

    /// <summary>For fields the converter guarantees present (declared required).</summary>
    public string GetRequiredText(string field) => (string)Get(field)!;

    public int? GetInt(string field) => Get(field) as int?;

    public int GetRequiredInt(string field) => (int)Get(field)!;

    public decimal? GetDecimal(string field) => Get(field) as decimal?;

    public decimal GetRequiredDecimal(string field) => (decimal)Get(field)!;

    public DateOnly? GetDate(string field) => Get(field) as DateOnly?;

    public DateOnly GetRequiredDate(string field) => (DateOnly)Get(field)!;

    public bool? GetBool(string field) => Get(field) as bool?;

    public string[] GetTextList(string field) => Get(field) as string[] ?? [];

    private object? Get(string field) => values.TryGetValue(field, out object? value) ? value : null;
}
