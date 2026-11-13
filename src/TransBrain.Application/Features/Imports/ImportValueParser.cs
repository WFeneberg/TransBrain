using System.Globalization;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Features.Imports;

/// <summary>
/// Converts the raw string cells of an import source into typed values. Tolerant on purpose:
/// exports from German office software ("13.11.2026", "1.234,56", "ja") must work exactly like
/// technical exports ("2026-11-13", "1234.56", "true") and Excel date serials ("46069").
/// </summary>
public static class ImportValueParser
{
    private static readonly string[] DateFormats =
    [
        "yyyy-MM-dd",
        "dd.MM.yyyy",
        "d.M.yyyy",
        "dd.MM.yy",
        "dd/MM/yyyy",
        "yyyyMMdd"
    ];

    private static readonly HashSet<string> TrueValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "1", "true", "wahr", "ja", "yes", "y", "j", "x"
    };

    private static readonly HashSet<string> FalseValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "0", "false", "falsch", "nein", "no", "n", "-"
    };

    // Excel's day-serial epoch. Day 1 is 1900-01-01; 9999-12-31 is 2958465.
    private static readonly DateOnly ExcelEpoch = new(1899, 12, 30);
    private const int MaxExcelSerial = 2_958_465;

    /// <summary>Parses one cell. Empty cells become a successful null; required-ness is the caller's concern.</summary>
    public static Result<object?> Parse(ImportFieldKind kind, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result<object?>.Success(null);
        }

        string value = raw.Trim();

        return kind switch
        {
            ImportFieldKind.Text => Result<object?>.Success(value),
            ImportFieldKind.Integer => ParseInteger(value),
            ImportFieldKind.Decimal => ParseDecimal(value),
            ImportFieldKind.Date => ParseDate(value),
            ImportFieldKind.Boolean => ParseBoolean(value),
            ImportFieldKind.TextList => ParseTextList(value),
            _ => Error.Validation("Import.UnknownFieldKind", $"Unknown field kind '{kind}'.")
        };
    }

    /// <summary>Formats a parsed value back into a stable display string for previews.</summary>
    public static string? Format(object? value) => value switch
    {
        null => null,
        string s => s,
        int i => i.ToString(CultureInfo.InvariantCulture),
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        string[] list => string.Join("; ", list),
        _ => value.ToString()
    };

    private static Result<object?> ParseInteger(string value)
    {
        string cleaned = value.Replace(" ", string.Empty).Replace("\u00A0", string.Empty);

        // For a whole-number field, separators in 3-digit groups are grouping, never decimals:
        // "1.234" and "1,234" both mean 1234 here, while a Decimal field reads them as 1.234.
        if (System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^-?\d{1,3}([.,']\d{3})+$"))
        {
            cleaned = cleaned.Replace(".", string.Empty).Replace(",", string.Empty).Replace("'", string.Empty);
        }

        Result<object?> asDecimal = ParseDecimal(cleaned);
        if (!asDecimal.IsSuccess)
        {
            return Error.Validation("Import.InvalidInteger", $"'{value}' is not a whole number.");
        }

        decimal parsed = (decimal)asDecimal.Value!;
        if (decimal.Truncate(parsed) != parsed || parsed is > int.MaxValue or < int.MinValue)
        {
            return Error.Validation("Import.InvalidInteger", $"'{value}' is not a whole number.");
        }

        return Result<object?>.Success((int)parsed);
    }

    private static Result<object?> ParseDecimal(string value)
    {
        // Spaces, non-breaking spaces and apostrophes only ever group digits ("1 234,56", "1'234.56").
        string cleaned = value
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty)
            .Replace("'", string.Empty);

        bool hasComma = cleaned.Contains(',');
        bool hasDot = cleaned.Contains('.');

        if (hasComma && hasDot)
        {
            // The last separator is the decimal one: "1.234,56" (de) and "1,234.56" (en) both work.
            if (cleaned.LastIndexOf(',') > cleaned.LastIndexOf('.'))
            {
                cleaned = cleaned.Replace(".", string.Empty).Replace(',', '.');
            }
            else
            {
                cleaned = cleaned.Replace(",", string.Empty);
            }
        }
        else if (hasComma)
        {
            // German convention: a lone comma is the decimal separator.
            cleaned = cleaned.Replace(',', '.');
        }
        else if (hasDot && System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"^-?\d{1,3}(\.\d{3}){2,}$"))
        {
            // "1.234.567" cannot be a decimal; it is German digit grouping.
            cleaned = cleaned.Replace(".", string.Empty);
        }

        if (decimal.TryParse(cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out decimal result))
        {
            return Result<object?>.Success(result);
        }

        return Error.Validation("Import.InvalidDecimal", $"'{value}' is not a number.");
    }

    private static Result<object?> ParseDate(string value)
    {
        if (DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out DateOnly date))
        {
            return Result<object?>.Success(date);
        }

        // ISO date-times ("2026-11-13T08:30:00", with or without offset) from JSON and databases.
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out DateTimeOffset dateTime))
        {
            return Result<object?>.Success(DateOnly.FromDateTime(dateTime.UtcDateTime));
        }

        // Excel stores dates as day serials; a bare number in the plausible range is one.
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int serial)
            && serial is >= 1 and <= MaxExcelSerial)
        {
            return Result<object?>.Success(ExcelEpoch.AddDays(serial));
        }

        return Error.Validation(
            "Import.InvalidDate",
            $"'{value}' is not a recognized date. Use e.g. '2026-11-13' or '13.11.2026'.");
    }

    private static Result<object?> ParseBoolean(string value)
    {
        if (TrueValues.Contains(value))
        {
            return Result<object?>.Success(true);
        }

        if (FalseValues.Contains(value))
        {
            return Result<object?>.Success(false);
        }

        return Error.Validation("Import.InvalidBoolean", $"'{value}' is not a recognized yes/no value.");
    }

    private static Result<object?> ParseTextList(string value)
    {
        string[] parts = value
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();

        return parts.Length == 0
            ? Result<object?>.Success(null)
            : Result<object?>.Success(parts);
    }
}
