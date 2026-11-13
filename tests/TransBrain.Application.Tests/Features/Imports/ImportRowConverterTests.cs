using AwesomeAssertions;
using TransBrain.Application.Features.Imports;
using TransBrain.Application.Features.Imports.Targets;
using TransBrain.Domain.Common;

namespace TransBrain.Application.Tests.Features.Imports;

public class ImportRowConverterTests
{
    private static readonly ImportTable Table = new(
        "Kunden",
        ["Firma", "Strasse", "PLZ", "Ort", "Land", "Zahlungsziel"],
        [["Huber GmbH", "Hauptstr. 1", "80331", "München", "DE", "30"]]);

    private static readonly IImportTargetDefinition Target = ImportTargetRegistry.Find("Customers")!;

    private static IReadOnlyList<ImportFieldMapping> ValidMappings =>
    [
        new("name", "Firma", null),
        new("street", "Strasse", null),
        new("postalCode", "PLZ", null),
        new("city", "Ort", null),
        new("country", "Land", null),
        new("paymentTermDays", "Zahlungsziel", null)
    ];

    [Fact]
    public void Resolve_ValidMappings_ResolvesColumnIndexesCaseInsensitively()
    {
        IReadOnlyList<ImportFieldMapping> mappings = [.. ValidMappings.Select(m => m with { SourceColumn = m.SourceColumn!.ToUpperInvariant() })];

        Result<IReadOnlyList<ResolvedFieldMapping>> result = ImportRowConverter.Resolve(Table, Target, mappings);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(6);
        result.Value[0].ColumnIndex.Should().Be(0);
        result.Value[5].ColumnIndex.Should().Be(5);
    }

    [Fact]
    public void Resolve_UnknownTargetField_ReturnsValidationError()
    {
        IReadOnlyList<ImportFieldMapping> mappings = [.. ValidMappings, new ImportFieldMapping("nickname", "Firma", null)];

        Result<IReadOnlyList<ResolvedFieldMapping>> result = ImportRowConverter.Resolve(Table, Target, mappings);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.UnknownTargetField");
    }

    [Fact]
    public void Resolve_DuplicateTargetField_ReturnsValidationError()
    {
        IReadOnlyList<ImportFieldMapping> mappings = [.. ValidMappings, new ImportFieldMapping("NAME", "Ort", null)];

        Result<IReadOnlyList<ResolvedFieldMapping>> result = ImportRowConverter.Resolve(Table, Target, mappings);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.DuplicateTargetField");
    }

    [Fact]
    public void Resolve_UnknownSourceColumn_ReturnsValidationError()
    {
        IReadOnlyList<ImportFieldMapping> mappings = [.. ValidMappings.Skip(1), new ImportFieldMapping("name", "Firmenname", null)];

        Result<IReadOnlyList<ResolvedFieldMapping>> result = ImportRowConverter.Resolve(Table, Target, mappings);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.UnknownSourceColumn");
    }

    [Fact]
    public void Resolve_RequiredFieldNotMapped_ReturnsValidationError()
    {
        IReadOnlyList<ImportFieldMapping> mappings = [.. ValidMappings.Where(m => m.TargetField != "country")];

        Result<IReadOnlyList<ResolvedFieldMapping>> result = ImportRowConverter.Resolve(Table, Target, mappings);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.RequiredFieldNotMapped");
        result.Error.Message.Should().Contain("country");
    }

    [Fact]
    public void Resolve_ConstantOnlyMapping_ResolvesWithoutColumn()
    {
        IReadOnlyList<ImportFieldMapping> mappings =
            [.. ValidMappings.Where(m => m.TargetField != "country"), new ImportFieldMapping("country", null, "DE")];

        Result<IReadOnlyList<ResolvedFieldMapping>> result = ImportRowConverter.Resolve(Table, Target, mappings);

        result.IsSuccess.Should().BeTrue();
        ResolvedFieldMapping country = result.Value.Single(m => m.Field.Name == "country");
        country.ColumnIndex.Should().Be(-1);
        country.ConstantValue.Should().Be("DE");
    }

    [Fact]
    public void Resolve_EmptyMappingRowFromUi_IsIgnored()
    {
        IReadOnlyList<ImportFieldMapping> mappings = [.. ValidMappings, new ImportFieldMapping("email", null, "  ")];

        Result<IReadOnlyList<ResolvedFieldMapping>> result = ImportRowConverter.Resolve(Table, Target, mappings);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotContain(m => m.Field.Name == "email");
    }

    [Fact]
    public void Convert_ValidRow_ProducesTypedValuesAndDisplayStrings()
    {
        IReadOnlyList<ResolvedFieldMapping> resolved = ImportRowConverter.Resolve(Table, Target, ValidMappings).Value;

        ImportRowConversion conversion = ImportRowConverter.Convert(resolved, Table.Rows[0]);

        conversion.IsValid.Should().BeTrue();
        conversion.Values!.GetRequiredText("name").Should().Be("Huber GmbH");
        conversion.Values.GetInt("paymentTermDays").Should().Be(30);
        conversion.DisplayValues["paymentTermDays"].Should().Be("30");
    }

    [Fact]
    public void Convert_ConstantFillsEmptyCell_ButCellValueWins()
    {
        IReadOnlyList<ImportFieldMapping> mappings =
            [.. ValidMappings.Where(m => m.TargetField != "country"), new ImportFieldMapping("country", "Land", "AT")];
        IReadOnlyList<ResolvedFieldMapping> resolved = ImportRowConverter.Resolve(Table, Target, mappings).Value;

        ImportRowConversion fromCell = ImportRowConverter.Convert(resolved, Table.Rows[0]);
        ImportRowConversion fromConstant = ImportRowConverter.Convert(
            resolved, ["Huber GmbH", "Hauptstr. 1", "80331", "München", "", "30"]);

        fromCell.Values!.GetRequiredText("country").Should().Be("DE");
        fromConstant.Values!.GetRequiredText("country").Should().Be("AT");
    }

    [Fact]
    public void Convert_UnparsableCell_CollectsErrorAndKeepsRawDisplayValue()
    {
        IReadOnlyList<ResolvedFieldMapping> resolved = ImportRowConverter.Resolve(Table, Target, ValidMappings).Value;

        ImportRowConversion conversion = ImportRowConverter.Convert(
            resolved, ["Huber GmbH", "Hauptstr. 1", "80331", "München", "DE", "bald"]);

        conversion.IsValid.Should().BeFalse();
        conversion.Values.Should().BeNull();
        conversion.Errors.Should().ContainSingle(e => e.StartsWith("paymentTermDays:"));
        conversion.DisplayValues["paymentTermDays"].Should().Be("bald");
    }

    [Fact]
    public void Convert_EmptyRequiredCell_CollectsError()
    {
        IReadOnlyList<ResolvedFieldMapping> resolved = ImportRowConverter.Resolve(Table, Target, ValidMappings).Value;

        ImportRowConversion conversion = ImportRowConverter.Convert(
            resolved, ["Huber GmbH", "Hauptstr. 1", "80331", "", "DE", "30"]);

        conversion.IsValid.Should().BeFalse();
        conversion.Errors.Should().ContainSingle(e => e.StartsWith("city:"));
    }

    [Fact]
    public void Convert_ShortRow_TreatsMissingCellsAsEmpty()
    {
        IReadOnlyList<ResolvedFieldMapping> resolved = ImportRowConverter.Resolve(Table, Target, ValidMappings).Value;

        ImportRowConversion conversion = ImportRowConverter.Convert(resolved, ["Huber GmbH"]);

        conversion.IsValid.Should().BeFalse();
        conversion.Errors.Should().Contain(e => e.StartsWith("street:"));
    }
}
