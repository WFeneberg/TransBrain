namespace TransBrain.Domain.Common;

public static class OptionalText
{
    /// <summary>Trims an optional text value and maps blank input to <c>null</c>.</summary>
    public static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
