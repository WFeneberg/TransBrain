using AwesomeAssertions;
using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Imports;

public class ImportValueParserTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyCell_ReturnsSuccessfulNull(string? raw)
    {
        Result<object?> result = ImportValueParser.Parse(ImportFieldKind.Integer, raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
    }

    [Fact]
    public void Parse_Text_TrimsValue()
    {
        Result<object?> result = ImportValueParser.Parse(ImportFieldKind.Text, "  Spedition Huber  ");

        result.Value.Should().Be("Spedition Huber");
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("-7", -7)]
    [InlineData("1.234", 1234)]
    [InlineData("1,234", 1234)]
    [InlineData("24000", 24000)]
    public void Parse_Integer_UnderstandsGroupedNumbers(string raw, int expected)
    {
        Result<object?> result = ImportValueParser.Parse(ImportFieldKind.Integer, raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("12.5")]
    [InlineData("abc")]
    [InlineData("2147483648")]
    public void Parse_Integer_RejectsNonIntegralValues(string raw)
    {
        Result<object?> result = ImportValueParser.Parse(ImportFieldKind.Integer, raw);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.InvalidInteger");
    }

    [Theory]
    [InlineData("1234.56", "1234.56")]
    [InlineData("1234,56", "1234.56")]
    [InlineData("1.234,56", "1234.56")]
    [InlineData("1,234.56", "1234.56")]
    [InlineData("1 234,56", "1234.56")]
    [InlineData("1'234.56", "1234.56")]
    [InlineData("1.234.567", "1234567")]
    [InlineData("-13,6", "-13.6")]
    public void Parse_Decimal_UnderstandsGermanAndEnglishFormats(string raw, string expected)
    {
        Result<object?> result = ImportValueParser.Parse(ImportFieldKind.Decimal, raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Parse_Decimal_RejectsText()
    {
        Result<object?> result = ImportValueParser.Parse(ImportFieldKind.Decimal, "dreizehn");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.InvalidDecimal");
    }

    [Theory]
    [InlineData("2026-11-13")]
    [InlineData("13.11.2026")]
    [InlineData("13/11/2026")]
    [InlineData("20261113")]
    [InlineData("2026-11-13T08:30:00")]
    [InlineData("2026-11-13T08:30:00+01:00")]
    public void Parse_Date_UnderstandsCommonFormats(string raw)
    {
        Result<object?> result = ImportValueParser.Parse(ImportFieldKind.Date, raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new DateOnly(2026, 11, 13));
    }

    [Fact]
    public void Parse_Date_UnderstandsExcelSerial()
    {
        // 46069 is 2026-02-16 in Excel's 1900 date system.
        Result<object?> result = ImportValueParser.Parse(ImportFieldKind.Date, "46069");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new DateOnly(2026, 2, 16));
    }

    [Theory]
    [InlineData("kein datum")]
    [InlineData("13.13.2026")]
    [InlineData("99999999")]
    public void Parse_Date_RejectsUnrecognizedValues(string raw)
    {
        Result<object?> result = ImportValueParser.Parse(ImportFieldKind.Date, raw);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.InvalidDate");
    }

    [Theory]
    [InlineData("ja", true)]
    [InlineData("JA", true)]
    [InlineData("wahr", true)]
    [InlineData("x", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("nein", false)]
    [InlineData("falsch", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("-", false)]
    public void Parse_Boolean_UnderstandsGermanAndEnglishWords(string raw, bool expected)
    {
        Result<object?> result = ImportValueParser.Parse(ImportFieldKind.Boolean, raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public void Parse_Boolean_RejectsUnknownWord()
    {
        Result<object?> result = ImportValueParser.Parse(ImportFieldKind.Boolean, "vielleicht");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.InvalidBoolean");
    }

    [Theory]
    [InlineData("C1;CE", new[] { "C1", "CE" })]
    [InlineData("C1, CE", new[] { "C1", "CE" })]
    [InlineData(" B ", new[] { "B" })]
    [InlineData("B;;C", new[] { "B", "C" })]
    public void Parse_TextList_SplitsOnSemicolonAndComma(string raw, string[] expected)
    {
        Result<object?> result = ImportValueParser.Parse(ImportFieldKind.TextList, raw);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Format_RendersStableDisplayStrings()
    {
        ImportValueParser.Format(null).Should().BeNull();
        ImportValueParser.Format("text").Should().Be("text");
        ImportValueParser.Format(42).Should().Be("42");
        ImportValueParser.Format(13.6m).Should().Be("13.6");
        ImportValueParser.Format(new DateOnly(2026, 11, 13)).Should().Be("2026-11-13");
        ImportValueParser.Format(true).Should().Be("true");
        ImportValueParser.Format(new[] { "C1", "CE" }).Should().Be("C1; CE");
    }
}
